using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Application.Reservation.GetReservationList;
using Rpom.Domain.Access;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Reservation;

public sealed class GetReservationListTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();

    private ApplicationDbContext _ctx = null!;
    private int _staffId, _counter1, _tableA;

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
    public async Task ListToday_ExpiredBooked_FlipsToNotArrived()
    {
        var past = DateTime.UtcNow.AddDays(-1);
        await CreateAt(past);

        var res = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, DateOnly.FromDateTime(past), null),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Items.Should().ContainSingle()
            .Which.Status.Should().Be(ReservationStatus.NotArrived);
        (await _ctx.Reservations.AsNoTracking().FirstAsync()).Status
            .Should().Be(ReservationStatus.NotArrived);
    }

    [Fact]
    public async Task ListToday_HoldingBooked_LabelledHolding()
    {
        await CreateAt(DateTime.UtcNow); // now ∈ window
        var res = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, DateOnly.FromDateTime(DateTime.UtcNow), null),
                CancellationToken.None);
        res.Value.Items.Single().Phase.Should().Be("HOLDING");
        res.Value.Items.Single().Status.Should().Be(ReservationStatus.Booked);
    }

    private async Task CreateAt(DateTime target) =>
        await new Rpom.Application.Reservation.CreateReservation.CreateReservation.Handler(
                _ctx, Staff(), Clock(), Config(), Substitute.For<IVersionService>())
            .Handle(new Rpom.Application.Reservation.CreateReservation.CreateReservation.Command(
                _counter1, target, "Long", "0901", 4, null, new[] { _tableA }), CancellationToken.None);

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
        var a1 = new Area { Counter = c1, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var tA = new Table { Area = a1, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        _ctx.AddRange(role, staff, c1, a1, tA);
        await _ctx.SaveChangesAsync();
        _staffId = staff.Id;
        _counter1 = c1.Id;
        _tableA = tA.Id;
    }
}
