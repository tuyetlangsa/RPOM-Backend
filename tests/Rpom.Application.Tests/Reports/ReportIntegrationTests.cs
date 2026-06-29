using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Reports;
using Rpom.Application.Reports.RevenueReport;
using Rpom.Application.Reports.DetailedRevenueReport;
using Rpom.Application.Reports.ItemReport;
using Rpom.Application.Reports.TopSellerReport;
using Rpom.Application.Reports.ShiftReport;
using Rpom.Application.Reports.ItemSalesDetail;
using Rpom.Application.Reports.StockAlertReport;
using Rpom.Application.Tickets.ListTickets;
using Rpom.Application.Tickets.GetTicketAuditLog;
using Rpom.Domain.Menu;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;
using Rpom.Domain.Operations;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Inventory;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Reports;

public sealed class ReportIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private IDateTimeProvider _clock = null!;
    private DateTime _now;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_db.GetConnectionString(),
                npgsqlOptions => npgsqlOptions
                    .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default)
                    .UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        _ctx = new ApplicationDbContext(options);
        await _ctx.Database.MigrateAsync();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(DateTime.UtcNow);
        _now = _clock.UtcNow;
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    // ==================== Revenue Report ====================

    [Fact]
    public async Task RevenueReport_WithClosedTickets_ReturnsCorrectMetrics()
    {
        var env = await SeedEnvAsync();
        var now = _now;

        for (int i = 0; i < 3; i++)
        {
            await SeedTicketAndInvoiceAsync(env, $"T-{i + 1}", 200000 + i * 50000, 206000 + i * 54000,
                (short)(2 + i), now.AddMinutes(-30 + i * 10), i == 0 ? 20000 : 0, 2 + i);
        }

        // Payment on first invoice
        var firstTicket = await _ctx.Tickets.FirstAsync(t => t.Code == "T-1");
        _ctx.TicketPaymentDetails.Add(new TicketPaymentDetail
        {
            TicketId = firstTicket.Id, PaymentMethodId = env.PayCash.Id,
            Amount = 150000, Status = TicketPaymentStatus.Success,
            ProcessedByStaffId = 1, ProcessedAt = now, CreatedAt = now
        });
        _ctx.TicketPaymentDetails.Add(new TicketPaymentDetail
        {
            TicketId = firstTicket.Id, PaymentMethodId = env.PayQr.Id,
            Amount = 56000, Status = TicketPaymentStatus.Success,
            ProcessedByStaffId = 1, ProcessedAt = now, CreatedAt = now
        });
        await _ctx.SaveChangesAsync();

        var handler = new RevenueReport.Handler(_ctx);
        var result = await handler.Handle(new RevenueReport.Query(
            new ReportFilter(null, null, null, null, null, null), "day"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BillCount.Should().Be(3);
        result.Value.TotalGuests.Should().Be(9);
        result.Value.CashAmount.Should().Be(150000);
        result.Value.QrAmount.Should().Be(56000);
        result.Value.DiscountedBillCount.Should().Be(1);
        result.Value.AverageBill.Should().BeGreaterThan(0);
        result.Value.Breakdown.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RevenueReport_WithCounterFilter_FiltersCorrectly()
    {
        var env1 = await SeedEnvAsync("Q1");
        var env2 = await SeedEnvAsync("Q2");
        var now = _now;

        await SeedTicketAndInvoiceAsync(env1, "T-Q1", 200000, 216000, 2, now);
        await SeedTicketAndInvoiceAsync(env2, "T-Q2", 50000, 54000, 1, now);

        var handler = new RevenueReport.Handler(_ctx);
        var result = await handler.Handle(new RevenueReport.Query(
            new ReportFilter(null, null, env1.Counter.Id, null, null, null)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BillCount.Should().Be(1);
    }

    [Fact]
    public async Task RevenueReport_ComparisonMetrics_Computed()
    {
        var env = await SeedEnvAsync();
        var now = _now;

        await SeedTicketAndInvoiceAsync(env, "T-TODAY", 300000, 324000, 2, now);
        await SeedTicketAndInvoiceAsync(env, "T-7DAGO", 150000, 162000, 1, now.AddDays(-7));

        var handler = new RevenueReport.Handler(_ctx);
        var result = await handler.Handle(new RevenueReport.Query(
            new ReportFilter(now.Date, now.Date.AddDays(1), null, null, null, null)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BillCount.Should().Be(1);
        result.Value.SameDowLastWeekRevenue.Should().BeGreaterThan(0);
    }

    // ==================== Detailed Revenue Report ====================

    [Fact]
    public async Task DetailedRevenueReport_ReturnsPaginatedBills()
    {
        var env = await SeedEnvAsync();
        var now = _now;

        for (int i = 0; i < 5; i++)
            await SeedTicketAndInvoiceAsync(env, $"T-DET-{i + 1}", 100000 + i * 20000, 108000 + i * 21600, (short)(i + 1), now.AddMinutes(-i * 5), linesCount: i + 1);

        var handler = new DetailedRevenueReport.Handler(_ctx);
        var result = await handler.Handle(new DetailedRevenueReport.Query(
            new ReportFilter(null, null, null, null, null, null), 1, 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Bills.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(5);
        result.Value.Bills[0].TicketCode.Should().Be("T-DET-1");
    }

    // ==================== Item Report ====================

    [Fact]
    public async Task ItemReport_AggregatesByItem()
    {
        var env = await SeedEnvAsync();
        var now = _now;

        var lines = new List<TicketInvoiceLine>
        {
            new() { ItemId = 1, ItemCode = "PHO", ItemName = "Pho bo", UomCode = "BAT", UomName = "Bat", Quantity = 5, UnitPrice = 50000, LineSubtotal = 250000, TotalAmount = 270000, DisplayOrder = 1, CreatedAt = now },
            new() { ItemId = 2, ItemCode = "COM", ItemName = "Com ga", UomCode = "DIA", UomName = "Dia", Quantity = 2, UnitPrice = 25000, LineSubtotal = 50000, TotalAmount = 54000, DisplayOrder = 2, CreatedAt = now }
        };
        await SeedTicketAndInvoiceAsync(env, "T-ITM-1", 300000, 324000, 2, now, lines: lines);

        var handler = new ItemReport.Handler(_ctx);
        var result = await handler.Handle(new ItemReport.Query(
            new ReportFilter(null, null, null, null, null, null)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(r => r.ItemCode == "PHO" && r.TotalQuantity == 5);
        result.Value.Should().Contain(r => r.ItemCode == "COM" && r.TotalQuantity == 2);
    }

    // ==================== Top Seller Report ====================

    [Fact]
    public async Task TopSellerReport_ReturnsTopNByRevenue()
    {
        var env = await SeedEnvAsync();
        var now = _now;

        var lines = new List<TicketInvoiceLine>
        {
            new() { ItemId = 1, ItemCode = "PHO", ItemName = "Pho", UomCode = "BAT", UomName = "Bat", Quantity = 10, UnitPrice = 50000, LineSubtotal = 500000, TotalAmount = 500000, DisplayOrder = 1, CreatedAt = now },
            new() { ItemId = 2, ItemCode = "COM", ItemName = "Com", UomCode = "DIA", UomName = "Dia", Quantity = 2, UnitPrice = 50000, LineSubtotal = 100000, TotalAmount = 100000, DisplayOrder = 2, CreatedAt = now },
            new() { ItemId = 3, ItemCode = "TRA", ItemName = "Tra", UomCode = "LY", UomName = "Ly", Quantity = 8, UnitPrice = 20000, LineSubtotal = 160000, TotalAmount = 160000, DisplayOrder = 3, CreatedAt = now }
        };
        await SeedTicketAndInvoiceAsync(env, "T-TOP-1", 760000, 760000, 2, now, lines: lines);

        var handler = new TopSellerReport.Handler(_ctx);
        var result = await handler.Handle(new TopSellerReport.Query(
            new ReportFilter(null, null, null, null, null, null), 3, "revenue"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value[0].ItemCode.Should().Be("PHO");
        result.Value[0].Rank.Should().Be(1);
    }

    // ==================== Shift Report ====================

    [Fact]
    public async Task ShiftReport_ReturnsVarianceAndRevenue()
    {
        var env = await SeedEnvAsync();
        var staff = await SeedStaffAsync("cashier1", "Le Van C");
        var now = _now;

        var session = new CashDrawerSession
        {
            CounterId = env.Counter.Id, ShiftId = env.Shift.Id,
            OpenedByStaffAccountId = staff.Id, OpenedAt = now.AddHours(-4),
            OpeningCash = 1000000, Status = CashDrawerStatus.Closed,
            ClosedByStaffAccountId = staff.Id, ClosedAt = now,
            ActualClosingCash = 1500000, ExpectedClosingCash = 1550000,
            Variance = -50000, CreatedAt = now, UpdatedAt = now
        };
        _ctx.CashDrawerSessions.Add(session);
        await _ctx.SaveChangesAsync();

        await SeedTicketAndInvoiceAsync(env, "T-SFT-1", 500000, 540000, 3, now.AddHours(-1));

        var handler = new ShiftReport.Handler(_ctx);
        var result = await handler.Handle(new ShiftReport.Query(
            new ReportFilter(now.Date.AddDays(-1), now.Date.AddDays(1), null, null, null, null)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].OpeningCash.Should().Be(1000000);
        result.Value[0].Variance.Should().Be(-50000);
        result.Value[0].TotalBills.Should().Be(1);
        result.Value[0].TotalRevenue.Should().Be(540000);
    }

    // ==================== Item Sales Detail ====================

    [Fact]
    public async Task ItemSalesDetail_ReturnsBillsWithNestedItems()
    {
        var env = await SeedEnvAsync();
        var now = _now;

        var lines = new List<TicketInvoiceLine>
        {
            new() { ItemId = 1, ItemCode = "PHO", ItemName = "Pho bo", UomCode = "BAT", UomName = "Bat", Quantity = 3, UnitPrice = 100000, VatPercent = 8m, LineSubtotal = 300000, TotalDiscount = 15000, TotalAmount = 324000, DisplayOrder = 1, CreatedAt = now },
            new() { ItemId = 2, ItemCode = "TRA", ItemName = "Tra da", UomCode = "LY", UomName = "Ly", Quantity = 2, UnitPrice = 50000, VatPercent = 8m, LineSubtotal = 100000, TotalDiscount = 5000, TotalAmount = 108000, DisplayOrder = 2, CreatedAt = now }
        };
        await SeedTicketAndInvoiceAsync(env, "T-ISD-1", 400000, 432000, 2, now, discountAmount: 20000, lines: lines);

        var handler = new ItemSalesDetail.Handler(_ctx);
        var result = await handler.Handle(new ItemSalesDetail.Query(
            new ReportFilter(null, null, null, null, null, null), null, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Bills.Should().HaveCount(1);
        result.Value.Bills[0].Items.Should().HaveCount(2);
        result.Value.Bills[0].Items.Should().Contain(i => i.ItemName == "Pho bo" && i.Quantity == 3);
    }

    // ==================== Stock Alert Report ====================

    [Fact]
    public async Task StockAlertReport_ReturnsLowStockItems()
    {
        var uom = await SeedUomAsync("KG", "Kilogram");
        var item = new Item
        {
            Code = "BEEF", Name = "Thit bo", IsActive = true, IsStockable = true,
            BaseUomId = uom.Id, LowStockThreshold = 10m,
            CreatedAt = _now, UpdatedAt = _now
        };
        _ctx.Items.Add(item);
        await _ctx.SaveChangesAsync();

        _ctx.ItemStocks.Add(new ItemStock
        {
            ItemId = item.Id, CurrentQty = 5m, LastMovementAt = _now, UpdatedAt = _now
        });
        await _ctx.SaveChangesAsync();

        var handler = new StockAlertReport.Handler(_ctx);
        var result = await handler.Handle(new StockAlertReport.Query(null, true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(r => r.ItemCode == "BEEF" && r.Status == "LOW" && r.CurrentQty == 5);
    }

    // ==================== Ticket Management ====================

    [Fact]
    public async Task ListTickets_ReturnsFilteredPaginatedResults()
    {
        var env = await SeedEnvAsync(withDrawer: true);
        var now = _now;

        // Open ticket
        _ctx.Tickets.Add(new Ticket
        {
            Code = "T-OPEN-1", CounterId = env.Counter.Id, AreaId = env.Area.Id,
            TableId = env.Table.Id, CashDrawerSessionId = env.Drawer!.Id, ShiftId = env.Shift.Id,
            Status = TicketStatus.Open, GuestCount = 2,
            Subtotal = 100000, TotalAmount = 108000,
            OpenedAt = now, CreatedAt = now, UpdatedAt = now
        });
        // Closed ticket
        _ctx.Tickets.Add(new Ticket
        {
            Code = "T-CLOSED-1", CounterId = env.Counter.Id, AreaId = env.Area.Id,
            TableId = env.Table.Id, CashDrawerSessionId = env.Drawer.Id, ShiftId = env.Shift.Id,
            Status = TicketStatus.Closed, GuestCount = 4,
            Subtotal = 300000, TotalAmount = 324000, PaidAmount = 324000,
            OpenedAt = now.AddHours(-2), ClosedAt = now.AddHours(-1),
            CreatedAt = now, UpdatedAt = now
        });
        await _ctx.SaveChangesAsync();

        var handler = new ListTickets.Handler(_ctx);

        var resultAll = await handler.Handle(
            new ListTickets.Query(null, null, null, null, null, null, null, 1, 50), CancellationToken.None);
        resultAll.IsSuccess.Should().BeTrue();
        resultAll.Value.TotalCount.Should().Be(2);

        var resultOpen = await handler.Handle(
            new ListTickets.Query(TicketStatus.Open, null, null, null, null, null, null, 1, 50), CancellationToken.None);
        resultOpen.IsSuccess.Should().BeTrue();
        resultOpen.Value.TotalCount.Should().Be(1);
        resultOpen.Value.Items.First().TicketCode.Should().Be("T-OPEN-1");
    }

    [Fact]
    public async Task GetTicketAuditLog_ReturnsHistoryForTicket()
    {
        var env = await SeedEnvAsync(withDrawer: true);
        var staff = await SeedStaffAsync("auditor1", "Tran Van B");
        var now = _now;

        var ticket = new Ticket
        {
            Code = "T-AUDIT-1", CounterId = env.Counter.Id, AreaId = env.Area.Id,
            TableId = env.Table.Id, CashDrawerSessionId = env.Drawer!.Id, ShiftId = env.Shift.Id,
            Status = TicketStatus.Open, GuestCount = 2,
            Subtotal = 200000, TotalAmount = 216000,
            OpenedAt = now, CreatedAt = now, UpdatedAt = now
        };
        _ctx.Tickets.Add(ticket);
        await _ctx.SaveChangesAsync();

        _ctx.AuditLogs.Add(new AuditLog
        {
            EntityType = "Ticket", EntityId = ticket.Id, Action = "OPEN",
            ActorFullName = staff.FullName, Timestamp = now,
            Summary = $"Ticket opened: {ticket.Code}"
        });
        _ctx.AuditLogs.Add(new AuditLog
        {
            EntityType = "Ticket", EntityId = ticket.Id, Action = "ADD_ITEMS",
            ActorFullName = staff.FullName, Timestamp = now.AddMinutes(5),
            Summary = "Added 2 items"
        });
        await _ctx.SaveChangesAsync();

        var handler = new GetTicketAuditLog.Handler(_ctx);
        var result = await handler.Handle(new GetTicketAuditLog.Query(ticket.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(a => a.Action == "OPEN");
        result.Value.Should().Contain(a => a.Action == "ADD_ITEMS");
    }

    [Fact]
    public async Task GetTicketAuditLog_NotFoundTicket_ReturnsFailure()
    {
        var handler = new GetTicketAuditLog.Handler(_ctx);
        var result = await handler.Handle(new GetTicketAuditLog.Query(99999), CancellationToken.None);
        result.IsFailure.Should().BeTrue();
    }

    // ==================== Helpers ====================

    private record Env(
        Counter Counter, Area Area, Table Table, Shift Shift,
        PaymentMethod PayCash, PaymentMethod PayQr,
        CashDrawerSession? Drawer = null);

    private async Task<Env> SeedEnvAsync(string counterName = "Q1", bool withDrawer = false)
    {
        var counter = await SeedCounterAsync(counterName);
        var area = await SeedAreaAsync(counter.Id, "Khu A");
        var table = await SeedTableAsync(area.Id, "B01");
        var shift = await SeedShiftAsync("S_MORNING");
        var payCash = await SeedPaymentMethodAsync("CASH", "Tien mat");
        var payQr = await SeedPaymentMethodAsync("QR", "QR");

        CashDrawerSession? drawer = null;
        if (withDrawer)
        {
            var staff = await SeedStaffAsync("drawer_" + counterName, "Drawer Staff");
            drawer = new CashDrawerSession
            {
                CounterId = counter.Id, ShiftId = shift.Id,
                OpenedByStaffAccountId = staff.Id, OpenedAt = _now.AddHours(-8),
                OpeningCash = 1000000, Status = CashDrawerStatus.Open,
                CreatedAt = _now, UpdatedAt = _now
            };
            _ctx.CashDrawerSessions.Add(drawer);
            await _ctx.SaveChangesAsync();
        }

        return new Env(counter, area, table, shift, payCash, payQr, drawer);
    }

    private async Task<(Ticket Ticket, TicketInvoice Invoice)> SeedTicketAndInvoiceAsync(
        Env env, string ticketCode, decimal subtotal, decimal total,
        short guestCount, DateTime closedAt,
        decimal discountAmount = 0, int linesCount = 0,
        List<TicketInvoiceLine>? lines = null)
    {
        CashDrawerSession drawer;
        if (env.Drawer != null)
        {
            drawer = env.Drawer;
        }
        else
        {
            // Reuse existing OPEN drawer for this counter to avoid unique constraint
            drawer = await _ctx.CashDrawerSessions
                .FirstOrDefaultAsync(d => d.CounterId == env.Counter.Id && d.Status == CashDrawerStatus.Open);
            if (drawer == null)
            {
                var staff = await SeedStaffAsync($"s_{ticketCode}", "Staff " + ticketCode);
                drawer = new CashDrawerSession
                {
                    CounterId = env.Counter.Id, ShiftId = env.Shift.Id,
                    OpenedByStaffAccountId = staff.Id, OpenedAt = _now.AddHours(-8),
                    OpeningCash = 1000000, Status = CashDrawerStatus.Open,
                    CreatedAt = _now, UpdatedAt = _now
                };
                _ctx.CashDrawerSessions.Add(drawer);
                await _ctx.SaveChangesAsync();
            }
        }

        var ticket = new Ticket
        {
            Code = ticketCode, CounterId = env.Counter.Id, AreaId = env.Area.Id,
            TableId = env.Table.Id, CashDrawerSessionId = drawer.Id, ShiftId = env.Shift.Id,
            Status = TicketStatus.Closed, GuestCount = guestCount,
            Subtotal = subtotal, DiscountAmount = discountAmount,
            TotalAmount = total, PaidAmount = total,
            OpenedAt = closedAt.AddHours(-1), ClosedAt = closedAt,
            CreatedAt = _now, UpdatedAt = _now
        };
        _ctx.Tickets.Add(ticket);
        await _ctx.SaveChangesAsync();

        var invoiceLines = lines ?? Enumerable.Range(0, linesCount).Select(i =>
            new TicketInvoiceLine
            {
                ItemId = i + 1, ItemCode = $"ITM{i + 1}", ItemName = $"Item {i + 1}",
                UomCode = "EA", UomName = "Each", Quantity = 1,
                UnitPrice = subtotal, LineSubtotal = subtotal,
                TotalAmount = total, DisplayOrder = i + 1, CreatedAt = _now
            }).ToList();

        var invoice = new TicketInvoice
        {
            TicketId = ticket.Id, TicketCode = ticket.Code,
            CounterId = env.Counter.Id, AreaId = env.Area.Id, ShiftId = env.Shift.Id,
            TableId = env.Table.Id, TableCode = "B01",
            GuestCount = guestCount, WaiterName = "Staff",
            ClosedByName = "Staff",
            Subtotal = subtotal, DiscountAmount = discountAmount,
            TotalAmount = total, PaidAmount = total,
            OpenedAt = ticket.OpenedAt, ClosedAt = closedAt, CreatedAt = _now,
            Lines = invoiceLines
        };
        _ctx.TicketInvoices.Add(invoice);
        await _ctx.SaveChangesAsync();

        return (ticket, invoice);
    }

    private async Task<Counter> SeedCounterAsync(string name)
    {
        var c = new Counter { Name = name, IsActive = true, CreatedAt = _now, UpdatedAt = _now };
        _ctx.Counters.Add(c);
        await _ctx.SaveChangesAsync();
        return c;
    }

    private async Task<Area> SeedAreaAsync(int counterId, string name)
    {
        var a = new Area { Name = name, CounterId = counterId, IsActive = true, CreatedAt = _now, UpdatedAt = _now };
        _ctx.Areas.Add(a);
        await _ctx.SaveChangesAsync();
        return a;
    }

    private async Task<Table> SeedTableAsync(int areaId, string code)
    {
        var t = new Table { Code = code, AreaId = areaId, SeatCount = 4, Status = "AVAILABLE", CreatedAt = _now, UpdatedAt = _now };
        _ctx.Tables.Add(t);
        await _ctx.SaveChangesAsync();
        return t;
    }

    private async Task<Shift> SeedShiftAsync(string code)
    {
        var existing = await _ctx.Shifts.FirstOrDefaultAsync(s => s.Code == code);
        if (existing != null) return existing;
        var s = new Shift { Code = code, Name = code, BeginTime = new TimeOnly(7, 0), EndTime = new TimeOnly(15, 0), IsActive = true, CreatedAt = _now, UpdatedAt = _now };
        _ctx.Shifts.Add(s);
        await _ctx.SaveChangesAsync();
        return s;
    }

    private async Task<Uom> SeedUomAsync(string code, string name)
    {
        var existing = await _ctx.Uoms.FirstOrDefaultAsync(u => u.Code == code);
        if (existing != null) return existing;
        var u = new Uom { Code = code, Name = name, CreatedAt = _now, UpdatedAt = _now };
        _ctx.Uoms.Add(u);
        await _ctx.SaveChangesAsync();
        return u;
    }

    private async Task<StaffAccount> SeedStaffAsync(string username, string fullName)
    {
        var role = await _ctx.Roles.FirstOrDefaultAsync();
        if (role == null)
        {
            role = new Role { Code = "TEST", Name = "Test Role", IsSystemRole = true, CreatedAt = _now, UpdatedAt = _now };
            _ctx.Roles.Add(role);
            await _ctx.SaveChangesAsync();
        }
        var s = new StaffAccount { Username = username, FullName = fullName, PasswordHash = "hash", RoleId = role.Id, IsActive = true, CreatedAt = _now, UpdatedAt = _now };
        _ctx.StaffAccounts.Add(s);
        await _ctx.SaveChangesAsync();
        return s;
    }

    private async Task<PaymentMethod> SeedPaymentMethodAsync(string code, string name)
    {
        var existing = await _ctx.PaymentMethods.FirstOrDefaultAsync(p => p.Code == code);
        if (existing != null) return existing;
        var pm = new PaymentMethod { Code = code, Name = name, IsActive = true, CreatedAt = _now, UpdatedAt = _now };
        _ctx.PaymentMethods.Add(pm);
        await _ctx.SaveChangesAsync();
        return pm;
    }
}
