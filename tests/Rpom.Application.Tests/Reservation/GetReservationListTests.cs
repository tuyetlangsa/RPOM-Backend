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
    private int _staffId, _counter1, _counter2, _tableA, _tableB, _tableC, _tableD;

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

    // ──────────────────────── existing tests ────────────────────────

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

    // ──────────────────────── new tests ────────────────────────

    [Fact]
    public async Task List_CounterScoped_ExcludesOtherCounter()
    {
        // _tableB on counter1, _tableC on counter2 — same target day; list counter1 → only counter1 booking.
        var target = DateTime.UtcNow.AddDays(2).AddHours(1);
        await CreateAtOn(_counter1, new[] { _tableB }, target);
        await CreateAtOn(_counter2, new[] { _tableC }, target);

        var date = DateOnly.FromDateTime(target);
        var res = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, date, null), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Items.Should().ContainSingle();
        res.Value.Items.Single().TableIds.Should().Contain(_tableB);
    }

    [Fact]
    public async Task List_StatusFilter_ReturnsOnlyMatching()
    {
        // Two counter1 bookings on day+3 (distinct tables): cancel one, keep the other BOOKED.
        var day3 = DateTime.UtcNow.AddDays(3);
        var targetCancel = new DateTime(day3.Year, day3.Month, day3.Day, 1, 0, 0, DateTimeKind.Utc);
        var targetBooked = new DateTime(day3.Year, day3.Month, day3.Day, 3, 0, 0, DateTimeKind.Utc);

        long cancelId = await CreateAtOn(_counter1, new[] { _tableD }, targetCancel);
        await CreateAtOn(_counter1, new[] { _tableA }, targetBooked);

        // Directly cancel the first reservation.
        var r = await _ctx.Reservations.FirstAsync(x => x.Id == cancelId);
        r.Status = ReservationStatus.Cancelled;
        await _ctx.SaveChangesAsync();

        var date = DateOnly.FromDateTime(targetCancel); // same calendar day as targetBooked

        // CANCELLED filter → only the cancelled one.
        var resCancelled = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, date, ReservationStatus.Cancelled),
                CancellationToken.None);
        resCancelled.IsSuccess.Should().BeTrue();
        resCancelled.Value.Items.Should().ContainSingle()
            .Which.Status.Should().Be(ReservationStatus.Cancelled);

        // BOOKED filter → only the kept one.
        var resBooked = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, date, ReservationStatus.Booked),
                CancellationToken.None);
        resBooked.IsSuccess.Should().BeTrue();
        resBooked.Value.Items.Should().ContainSingle()
            .Which.Status.Should().Be(ReservationStatus.Booked);
    }

    [Fact]
    public async Task List_DateFilter_ExcludesOtherDays()
    {
        // _tableD booked on day+5 and day+6; list day+5 → only that day's booking.
        var day5 = DateTime.UtcNow.AddDays(5);
        var targetToday = new DateTime(day5.Year, day5.Month, day5.Day, 10, 0, 0, DateTimeKind.Utc);
        var targetTomorrow = targetToday.AddDays(1);

        await CreateAtOn(_counter1, new[] { _tableD }, targetToday);
        await CreateAtOn(_counter1, new[] { _tableD }, targetTomorrow);

        var res = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, DateOnly.FromDateTime(targetToday), null),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Items.Should().ContainSingle()
            .Which.TableIds.Should().Contain(_tableD);
    }

    [Fact]
    public async Task List_IncludesTableIds()
    {
        // Multi-table booking on _tableA + _tableB; item.TableIds must contain both.
        var day8 = DateTime.UtcNow.AddDays(8);
        var target = new DateTime(day8.Year, day8.Month, day8.Day, 10, 0, 0, DateTimeKind.Utc);
        await CreateAtOn(_counter1, new[] { _tableA, _tableB }, target);

        var res = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, DateOnly.FromDateTime(target), null),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var item = res.Value.Items.Should().ContainSingle().Which;
        item.TableIds.Should().Contain(_tableA);
        item.TableIds.Should().Contain(_tableB);
        item.TableIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_ArrivedReservation_HasNullPhase()
    {
        // ARRIVED reservation: Phase must be null (only BOOKED gets a phase).
        var day10 = DateTime.UtcNow.AddDays(10);
        var target = new DateTime(day10.Year, day10.Month, day10.Day, 10, 0, 0, DateTimeKind.Utc);
        long rid = await CreateAtOn(_counter1, new[] { _tableD }, target);

        var r = await _ctx.Reservations.FirstAsync(x => x.Id == rid);
        r.Status = ReservationStatus.Arrived;
        await _ctx.SaveChangesAsync();

        var res = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, DateOnly.FromDateTime(target), null),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var item = res.Value.Items.Should().ContainSingle().Which;
        item.Status.Should().Be(ReservationStatus.Arrived);
        item.Phase.Should().BeNull();
    }

    [Fact]
    public async Task List_FutureBooking_LabelledPending()
    {
        // target = now+2h → window start = now+90min > now → Phase == "PENDING".
        var target = DateTime.UtcNow.AddHours(2);
        await CreateAtOn(_counter1, new[] { _tableD }, target);

        var res = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, DateOnly.FromDateTime(target), null),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        // Use First(predicate): other BOOKED items may be present on the same counter+day.
        var item = res.Value.Items.First(i => i.TableIds.Contains(_tableD));
        item.Phase.Should().Be("PENDING");
        item.Status.Should().Be(ReservationStatus.Booked);
    }

    // ──────────────────────── helpers ────────────────────────

    private async Task CreateAt(DateTime target) =>
        await new Rpom.Application.Reservation.CreateReservation.CreateReservation.Handler(
                _ctx, Staff(), Clock(), Config(), Substitute.For<IVersionService>())
            .Handle(new Rpom.Application.Reservation.CreateReservation.CreateReservation.Command(
                _counter1, target, "Long", "0901", 4, null, new[] { _tableA }), CancellationToken.None);

    private async Task<long> CreateAtOn(int counterId, int[] tableIds, DateTime target)
    {
        var result = await new Rpom.Application.Reservation.CreateReservation.CreateReservation.Handler(
                _ctx, Staff(), Clock(), Config(), Substitute.For<IVersionService>())
            .Handle(new Rpom.Application.Reservation.CreateReservation.CreateReservation.Command(
                counterId, target, "Long", "0901", 4, null, tableIds), CancellationToken.None);
        result.IsSuccess.Should().BeTrue($"CreateAtOn failed: counter={counterId}, target={target}");
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
        var tC = new Table { Area = a2, Code = "T20", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tD = new Table { Area = a1, Code = "T03", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        _ctx.AddRange(role, staff, c1, c2, a1, a2, tA, tB, tC, tD);
        await _ctx.SaveChangesAsync();
        _staffId = staff.Id;
        _counter1 = c1.Id;
        _counter2 = c2.Id;
        _tableA = tA.Id;
        _tableB = tB.Id;
        _tableC = tC.Id;
        _tableD = tD.Id;
    }
}
