using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Reservation.CancelReservation;
using Rpom.Domain.Access;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Reservation;

public sealed class CancelReservationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId, _counter1, _tableA;
    private int _reasonId;

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
    public async Task Cancel_BookedReservation_SetsCancelled()
    {
        long rid = await CreateBooking(DateTime.UtcNow.AddDays(1), new[] { _tableA });
        var res = await Cancel().Handle(
            new CancelReservation.Command(rid, _reasonId, "khách huỷ"), CancellationToken.None);
        res.IsSuccess.Should().BeTrue();
        var r = await _ctx.Reservations.AsNoTracking().FirstAsync(x => x.Id == rid);
        r.Status.Should().Be(ReservationStatus.Cancelled);
        r.CancellationReasonId.Should().Be(_reasonId);
        r.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_NonBooked_Fails()
    {
        long rid = await CreateBooking(DateTime.UtcNow.AddDays(2), new[] { _tableA });
        var r = await _ctx.Reservations.FirstAsync(x => x.Id == rid);
        r.Status = ReservationStatus.Cancelled;
        await _ctx.SaveChangesAsync();

        var res = await Cancel().Handle(
            new CancelReservation.Command(rid, _reasonId, null), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.NotBooked");
    }

    [Fact]
    public async Task Cancel_NotFound_Fails()
    {
        var res = await Cancel().Handle(
            new CancelReservation.Command(99999L, _reasonId, null), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.NotFound");
    }

    [Fact]
    public async Task Cancel_UnknownReason_Fails()
    {
        long rid = await CreateBooking(DateTime.UtcNow.AddDays(3), new[] { _tableA });
        var res = await Cancel().Handle(
            new CancelReservation.Command(rid, 99999, null), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("CancellationReason.NotFound");
    }

    private CancelReservation.Handler Cancel() => new(_ctx, Staff(), Clock(), Version());

    private async Task<long> CreateBooking(DateTime target, int[] tableIds) =>
        (await new Rpom.Application.Reservation.CreateReservation.CreateReservation.Handler(
                _ctx, Staff(), Clock(), Config(), Version())
            .Handle(new Rpom.Application.Reservation.CreateReservation.CreateReservation.Command(
                _counter1, target, "Long", "0901", 4, null, tableIds), CancellationToken.None)).Value.ReservationId;

    private static IVersionService Version() => Substitute.For<IVersionService>();

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
        var a1 = new Area { Counter = c1, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var tA = new Table { Area = a1, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var reason = new CancellationReason { Code = "CUS_CHANGE_MIND", Name = "Khách đổi ý", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        _ctx.AddRange(role, staff, c1, a1, tA, reason);
        await _ctx.SaveChangesAsync();
        _staffId = staff.Id;
        _counter1 = c1.Id;
        _tableA = tA.Id;
        _reasonId = reason.Id;
    }
}
