using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Cashier.AcquireTableLock;
using Rpom.Application.Reservation.CreateReservation;
using Rpom.Application.Reservation.SeatReservation;
using Rpom.Domain.Access;
using Rpom.Domain.Operations;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;
using Rpom.Infrastructure.Database;
using Rpom.Infrastructure.Tables;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Reservation;

/// <summary>UC-R3 — SeatReservation: opens per-table tickets and marks reservation ARRIVED.</summary>
public sealed class SeatReservationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;

    // counter1 + its tables (counter1 has an open cash drawer)
    private int _staffId, _counter1, _tableA, _tableB, _tableD;

    // counter2 + its table (counter2 has NO cash drawer — used for NoOpenCashDrawer test)
    private int _counter2, _tableC;

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

    // ---------- existing tests (untouched) ----------

    [Fact]
    public async Task Seat_OpensOneTicketPerSelectedTable_AndMarksArrived()
    {
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA, _tableB });
        await Lock(_tableA); await Lock(_tableB);

        var res = await Seat().Handle(new SeatReservation.Command(rid, new[]
        {
            new SeatReservation.SeatTable(_tableA, 3),
            new SeatReservation.SeatTable(_tableB, 3)
        }), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Tickets.Should().HaveCount(2);
        (await _ctx.Reservations.AsNoTracking().FirstAsync(x => x.Id == rid)).Status
            .Should().Be(ReservationStatus.Arrived);
        (await _ctx.Tickets.CountAsync(t => t.ReservationId == rid)).Should().Be(2);
        (await _ctx.Tables.Where(t => t.Id == _tableA).Select(t => t.Status).FirstAsync())
            .Should().Be(TableStatus.Occupied);
    }

    [Fact]
    public async Task Seat_PastWindow_Fails()
    {
        long rid = await CreateBooking(DateTime.UtcNow.AddDays(-1), new[] { _tableA });
        await Lock(_tableA);
        var res = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableA, 2) }), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.WindowExpired");
    }

    // ---------- new tests ----------

    /// <summary>
    ///     D5/D6 flexibility rule: the handler seats the ACTUAL presented table, which may differ
    ///     from the booked table. The ticket links to the actual table, not the booked one.
    /// </summary>
    [Fact]
    public async Task Seat_OnDifferentTableThanBooked_LinksActualSeatedTable()
    {
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA });
        await Lock(_tableD);

        var res = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableD, 2) }), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Tickets.Should().HaveCount(1);
        res.Value.Tickets[0].TableId.Should().Be(_tableD);
        (await _ctx.Tickets.Where(t => t.ReservationId == rid).Select(t => t.TableId).FirstAsync())
            .Should().Be(_tableD);
        (await _ctx.Reservations.AsNoTracking().FirstAsync(r => r.Id == rid)).Status
            .Should().Be(ReservationStatus.Arrived);
        (await _ctx.Tables.Where(t => t.Id == _tableD).Select(t => t.Status).FirstAsync())
            .Should().Be(TableStatus.Occupied);
    }

    /// <summary>
    ///     Seating a subset of the booked tables is allowed; only selected tables get tickets
    ///     and the non-selected booked table stays Available.
    /// </summary>
    [Fact]
    public async Task Seat_SubsetOfBookedTables_OpensOnlySelected()
    {
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA, _tableB });
        await Lock(_tableA);

        var res = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableA, 2) }), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Tickets.Should().HaveCount(1);
        res.Value.Tickets[0].TableId.Should().Be(_tableA);
        (await _ctx.Reservations.AsNoTracking().FirstAsync(r => r.Id == rid)).Status
            .Should().Be(ReservationStatus.Arrived);
        (await _ctx.Tables.Where(t => t.Id == _tableB).Select(t => t.Status).FirstAsync())
            .Should().Be(TableStatus.Available);
    }

    /// <summary>
    ///     Seating without acquiring the operation lock first is rejected (BR-lock).
    /// </summary>
    [Fact]
    public async Task Seat_TableNotLocked_Fails()
    {
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA });
        // intentionally do NOT call Lock(_tableA)

        var res = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableA, 2) }), CancellationToken.None);

        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("TableLock.NotHeld");
    }

    /// <summary>
    ///     Seating on a table that belongs to a different counter than the reservation is rejected.
    /// </summary>
    [Fact]
    public async Task Seat_CrossCounter_Fails()
    {
        // Reservation is on counter1; tableC is on counter2.
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA });
        await Lock(_tableC);

        var res = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableC, 2) }), CancellationToken.None);

        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.SeatTablesCrossCounter");
    }

    /// <summary>
    ///     Seating a non-existent reservation id is rejected immediately.
    /// </summary>
    [Fact]
    public async Task Seat_NotFound_Fails()
    {
        // No lock needed — handler checks reservation existence first.
        var res = await Seat().Handle(new SeatReservation.Command(999999L,
            new[] { new SeatReservation.SeatTable(_tableA, 2) }), CancellationToken.None);

        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.NotFound");
    }

    /// <summary>
    ///     A second seat attempt on the same reservation fails because it is now ARRIVED.
    /// </summary>
    [Fact]
    public async Task Seat_AlreadyArrived_Fails()
    {
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA });

        await Lock(_tableA);
        var first = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableA, 2) }), CancellationToken.None);
        first.IsSuccess.Should().BeTrue("first seat should succeed");

        // Lock is still valid (same staff); re-acquire heartbeat before second attempt.
        await Lock(_tableA);
        var res = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableA, 2) }), CancellationToken.None);

        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.NotBooked");
    }

    /// <summary>
    ///     A cancelled reservation cannot be seated (status != BOOKED).
    /// </summary>
    [Fact]
    public async Task Seat_Cancelled_Fails()
    {
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA });

        // Force the reservation into Cancelled state directly.
        var reservation = await _ctx.Reservations.FirstAsync(r => r.Id == rid);
        reservation.Status = ReservationStatus.Cancelled;
        reservation.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();

        await Lock(_tableA);

        var res = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableA, 2) }), CancellationToken.None);

        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.NotBooked");
    }

    /// <summary>
    ///     When the reservation's counter has no open cash drawer, seating is rejected.
    /// </summary>
    [Fact]
    public async Task Seat_NoOpenCashDrawer_Fails()
    {
        // Reservation on counter2 (tableC) — counter2 has no drawer.
        long rid = await CreateBookingOnCounter(_counter2, DateTime.UtcNow, new[] { _tableC });
        await Lock(_tableC);

        var res = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableC, 2) }), CancellationToken.None);

        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Ticket.NoOpenCashDrawer");
    }

    // ---------- helpers ----------

    private SeatReservation.Handler Seat() => new(_ctx, Staff(), Clock(), Guard(), Config(), Version());
    private TableOperationGuard Guard() => new(_ctx, Clock(), Config());

    private async Task Lock(int tableId) =>
        await new AcquireTableLock.Handler(_ctx, Staff(), Clock(), Version(), Config())
            .Handle(new AcquireTableLock.Command(tableId), CancellationToken.None);

    private async Task<long> CreateBooking(DateTime target, int[] tableIds) =>
        (await new CreateReservation.Handler(_ctx, Staff(), Clock(), Config(), Version())
            .Handle(new CreateReservation.Command(
                _counter1, target, "Long", "0901", 6, null, tableIds), CancellationToken.None)).Value.ReservationId;

    private async Task<long> CreateBookingOnCounter(int counterId, DateTime target, int[] tableIds) =>
        (await new CreateReservation.Handler(_ctx, Staff(), Clock(), Config(), Version())
            .Handle(new CreateReservation.Command(
                counterId, target, "Long", "0901", 6, null, tableIds), CancellationToken.None)).Value.ReservationId;

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

    private static IVersionService Version() => Substitute.For<IVersionService>();

    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount { Username = "c", PasswordHash = "x", FullName = "Thu ngân", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };

        // counter1 — has open cash drawer
        var counter1 = new Counter { Name = "C1", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var a1 = new Area { Counter = counter1, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var tA = new Table { Area = a1, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tB = new Table { Area = a1, Code = "T02", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        // tableD: a second free table on counter1, not pre-booked in any test
        var tD = new Table { Area = a1, Code = "T04", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };

        // counter2 — deliberately has NO cash drawer
        var counter2 = new Counter { Name = "C2", DisplayOrder = 2, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var a2 = new Area { Counter = counter2, Name = "B", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var tC = new Table { Area = a2, Code = "T03", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var shift = new Shift { Code = "S1", Name = "Sáng", BeginTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59), IsActive = true, CreatedAt = now, UpdatedAt = now };

        _ctx.AddRange(role, staff, counter1, a1, tA, tB, tD, counter2, a2, tC, shift);
        await _ctx.SaveChangesAsync();

        // Open drawer only on counter1.
        var drawer = new CashDrawerSession
        {
            Counter = counter1,
            OpenedByStaffAccountId = staff.Id,
            ShiftId = shift.Id,
            Status = CashDrawerStatus.Open,
            OpeningCash = 0m,
            OpenedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _ctx.Add(drawer);
        await _ctx.SaveChangesAsync();

        _staffId = staff.Id;
        _counter1 = counter1.Id;
        _tableA = tA.Id;
        _tableB = tB.Id;
        _tableD = tD.Id;
        _counter2 = counter2.Id;
        _tableC = tC.Id;
    }
}
