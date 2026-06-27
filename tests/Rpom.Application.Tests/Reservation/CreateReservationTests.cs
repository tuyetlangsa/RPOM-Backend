using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Application.Reservation.CreateReservation;
using Rpom.Domain.Access;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Reservation;

public sealed class CreateReservationTests : IAsyncLifetime
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
    public async Task MultiTable_SameCounter_CreatesBookedWithTables()
    {
        var res = await Handler().Handle(
            new CreateReservation.Command(_counter1, Target, "Long", "0901", 6, "sinh nhật",
                new[] { _tableA, _tableB }), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var r = await _ctx.Reservations.Include(x => x.ReservationTables)
            .FirstAsync(x => x.Id == res.Value.ReservationId);
        r.Status.Should().Be(ReservationStatus.Booked);
        r.CounterId.Should().Be(_counter1);
        r.Code.Should().StartWith("R-2026-");
        r.ReservationTables.Select(t => t.TableId).Should().BeEquivalentTo(new[] { _tableA, _tableB });
    }

    [Fact]
    public async Task CrossCounter_Fails()
    {
        var res = await Handler().Handle(
            new CreateReservation.Command(_counter1, Target, "Long", "0901", 4, null,
                new[] { _tableA, _tableOtherCounter }), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.TablesCrossCounter");
    }

    [Fact]
    public async Task OverlappingWindowOnSameTable_Fails()
    {
        await Handler().Handle(new CreateReservation.Command(
            _counter1, Target, "A", "1", 2, null, new[] { _tableA }), CancellationToken.None);
        var res = await Handler().Handle(new CreateReservation.Command(
            _counter1, Target.AddMinutes(45), "B", "2", 2, null, new[] { _tableA }), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.TableOverlap");
    }

    private CreateReservation.Handler Handler() => new(_ctx, Staff(), Clock(), Config(), Version());

    private static IVersionService Version() => Substitute.For<IVersionService>();

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
