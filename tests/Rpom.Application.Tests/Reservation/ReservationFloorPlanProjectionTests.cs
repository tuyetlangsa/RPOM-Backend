using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Application.Reservation.GetReservationFloorPlanProjection;
using Rpom.Domain.Access;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Reservation;

public sealed class ReservationFloorPlanProjectionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId, _counter1, _tableA, _tableB, _tableOtherCounter;
    private static readonly DateTime Target = new(2026, 6, 28, 18, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_db.GetConnectionString(),
                o => o.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default).UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        _ctx = new ApplicationDbContext(options);
        await _ctx.Database.MigrateAsync();
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Projection_FlagsOverlapTablesAndReturnsReservations()
    {
        // Seed one BOOKED reservation on tableA at 18:00 via the Create handler.
        await new Rpom.Application.Reservation.CreateReservation.CreateReservation.Handler(
                _ctx, Staff(), Clock(), Config(), Substitute.For<IVersionService>())
            .Handle(new Rpom.Application.Reservation.CreateReservation.CreateReservation.Command(
                _counter1, Target, "Long", "0901", 4, null, new[] { _tableA }), CancellationToken.None);

        var res = await new GetReservationFloorPlanProjection.Handler(_ctx, Config())
            .Handle(new GetReservationFloorPlanProjection.Query(_counter1, Target.AddMinutes(45)),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var tA = res.Value.Tables.Single(t => t.TableId == _tableA);
        tA.IsReservedOverlap.Should().BeTrue();   // 18:45 window overlaps 18:00 window
        var tB = res.Value.Tables.Single(t => t.TableId == _tableB);
        tB.IsReservedOverlap.Should().BeFalse();
        res.Value.OverlappingReservations.Should().ContainSingle();
    }

    // ──────────────────────── new tests ────────────────────────

    [Fact]
    public async Task Projection_CounterNotFound_Fails()
    {
        var res = await new GetReservationFloorPlanProjection.Handler(_ctx, Config())
            .Handle(new GetReservationFloorPlanProjection.Query(999999, Target), CancellationToken.None);

        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Counter.NotFound");
    }

    [Fact]
    public async Task Projection_NoBookings_AllTablesFree()
    {
        // Query far in the future where no reservations exist → all tables free.
        var res = await new GetReservationFloorPlanProjection.Handler(_ctx, Config())
            .Handle(new GetReservationFloorPlanProjection.Query(_counter1, Target.AddDays(100)),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Tables.Should().AllSatisfy(t => t.IsReservedOverlap.Should().BeFalse());
        res.Value.OverlappingReservations.Should().BeEmpty();
    }

    [Fact]
    public async Task Projection_CancelledReservation_NotFlagged()
    {
        // Book _tableA at Target+2 days then cancel it; only BOOKED matters for overlap.
        var cancelTarget = Target.AddDays(2);
        long rid = await BookAt(new[] { _tableA }, cancelTarget);

        var r = await _ctx.Reservations.FirstAsync(x => x.Id == rid);
        r.Status = ReservationStatus.Cancelled;
        await _ctx.SaveChangesAsync();

        var res = await new GetReservationFloorPlanProjection.Handler(_ctx, Config())
            .Handle(new GetReservationFloorPlanProjection.Query(_counter1, cancelTarget.AddMinutes(45)),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Tables.Single(t => t.TableId == _tableA).IsReservedOverlap.Should().BeFalse();
        res.Value.OverlappingReservations.Should().BeEmpty();
    }

    [Fact]
    public async Task Projection_MultiTableReservation_FlagsAllItsTables()
    {
        // One booking covering two tables: both must be flagged.
        var multiTarget = Target.AddDays(3);
        await BookAt(new[] { _tableA, _tableB }, multiTarget);

        var res = await new GetReservationFloorPlanProjection.Handler(_ctx, Config())
            .Handle(new GetReservationFloorPlanProjection.Query(_counter1, multiTarget.AddMinutes(15)),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Tables.Single(t => t.TableId == _tableA).IsReservedOverlap.Should().BeTrue();
        res.Value.Tables.Single(t => t.TableId == _tableB).IsReservedOverlap.Should().BeTrue();
        res.Value.OverlappingReservations.Should().ContainSingle();
    }

    // ──────────────────────── helpers ────────────────────────

    private async Task<long> BookAt(int[] tableIds, DateTime target)
    {
        var result = await new Rpom.Application.Reservation.CreateReservation.CreateReservation.Handler(
                _ctx, Staff(), Clock(), Config(), Substitute.For<IVersionService>())
            .Handle(new Rpom.Application.Reservation.CreateReservation.CreateReservation.Command(
                _counter1, target, "Long", "0901", 4, null, tableIds), CancellationToken.None);
        result.IsSuccess.Should().BeTrue($"BookAt failed: target={target}");
        return result.Value.ReservationId;
    }

    // GetIntAsync is an extension method — mock the underlying GetAsync returning null
    // so the extension method falls back to the default value passed in by the handler.
    private static IConfigValueService Config()
    {
        var c = Substitute.For<IConfigValueService>();
        c.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        return c;
    }

    private ICurrentStaff Staff()
    {
        var s = Substitute.For<ICurrentStaff>();
        s.StaffAccountId.Returns(_staffId);
        return s;
    }

    private static IDateTimeProvider Clock()
    {
        var c = Substitute.For<IDateTimeProvider>();
        c.UtcNow.Returns(_ => DateTime.UtcNow);
        return c;
    }

    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount { Username = "c", PasswordHash = "x", FullName = "Thu ngân", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var c1 = new Counter { Name = "C1", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var c2 = new Counter { Name = "C2", DisplayOrder = 2, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var a1 = new Area { Counter = c1, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var a2 = new Area { Counter = c2, Name = "B", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var tA = new Table { Area = a1, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tB = new Table { Area = a1, Code = "T02", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tOther = new Table { Area = a2, Code = "T20", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        _ctx.AddRange(role, staff, c1, c2, a1, a2, tA, tB, tOther);
        await _ctx.SaveChangesAsync();
        _staffId = staff.Id; _counter1 = c1.Id; _tableA = tA.Id; _tableB = tB.Id; _tableOtherCounter = tOther.Id;
    }
}
