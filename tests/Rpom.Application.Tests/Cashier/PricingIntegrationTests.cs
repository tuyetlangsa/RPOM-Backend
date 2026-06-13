using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Cashier.AcquireTableLock;
using Rpom.Application.Cashier.AddCartItem;
using Rpom.Application.Cashier.CancelOrderItem;
using Rpom.Application.Cashier.OpenTicket;
using Rpom.Application.Cashier.SendOrder;
using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;
using Rpom.Infrastructure.Database;
using Rpom.Infrastructure.Pricing;
using Rpom.Infrastructure.Tables;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Cashier;

public sealed class PricingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId, _tableId, _shiftId;
    private int _phoId, _biaId, _cocaId, _traDaId, _caPheId;

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
    public async Task VatExcludedItem_HasCorrectPricing()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        var add = await AddItem(ticket, _phoId, 2);
        add.IsSuccess.Should().BeTrue();
        add.Value.LineTotal.Should().Be(108_000m);
    }

    [Fact]
    public async Task VatIncludedItem_HasCorrectPricing()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        var add = await AddItem(ticket, _traDaId, 1);
        add.IsSuccess.Should().BeTrue();
        add.Value.LineTotal.Should().Be(5_000m);
    }

    [Fact]
    public async Task VatIncludedItem_MixedWithExcluded_SendOrder_TotalsMatch()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 2);
        await AddItem(ticket, _traDaId, 1);
        var send = await Send(ticket);
        // Pho: 2x50000=100000 +8%VAT=8000=108000; TraDa: 5000; Total=113000
        send.Value.TotalAmount.Should().Be(113_000m);
    }

    [Fact]
    public async Task DiscountPercent_AutoApply_BillAboveThreshold()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        for (int i = 0; i < 6; i++) await AddItem(ticket, _phoId, 1);
        var send = await Send(ticket);
        // Subtotal=300000, Discount 10%=30000, Taxable=270000, VAT 8%=21600, Total=291600
        send.Value.TotalAmount.Should().Be(291_600m);
    }

    [Fact]
    public async Task DiscountFixed_AutoApply_BillAboveThreshold()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        for (int i = 0; i < 20; i++) await AddItem(ticket, _phoId, 1);
        var send = await Send(ticket);
        // Subtotal=1000000, Discount=-100000, 900000+8%VAT=72000, Total=972000
        send.Value.TotalAmount.Should().Be(972_000m);
    }

    [Fact]
    public async Task CancelOrderItem_RecomputesTicket()
    {
        await AcquireLock();
        var ticket = await OpenTicket();

        // Add 3 Pho items as distinct lines (distinct notes prevent the note-free merge) to get
        // 3 OrderItems. Notes don't affect price.
        await AddItem(ticket, _phoId, 1, "bàn 1");
        await AddItem(ticket, _phoId, 1, "bàn 2");
        await AddItem(ticket, _phoId, 1, "bàn 3");
        var send = await Send(ticket);
        // 3 * 50000 = 150000 + 8% VAT = 162000
        send.Value.TotalAmount.Should().Be(162_000m);

        var items = await _ctx.OrderItems
            .Where(oi => oi.TicketId == ticket && oi.Status == OrderItemStatus.Pending)
            .ToListAsync();
        items.Count.Should().Be(3);

        // Cancel 1 item.
        var cancel = await new CancelOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new CancelOrderItem.Command(ticket, new List<long> { items[0].Id }), CancellationToken.None);
        cancel.IsSuccess.Should().BeTrue();

        // Remaining 2 items: 100000 + 8% VAT = 108000
        var final = await _ctx.Tickets.AsNoTracking().FirstAsync(t => t.Id == ticket);
        final.TotalAmount.Should().Be(108_000m);
    }

    [Fact]
    public async Task PartialSend_KeptItemsInNewDraft()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        var a = await AddItem(ticket, _phoId, 2);
        var b = await AddItem(ticket, _cocaId, 1);

        var send = await new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Rc(), Version())
            .Handle(new SendOrder.Command(ticket, new List<long> { a.Value.CartItemId }), CancellationToken.None);
        send.IsSuccess.Should().BeTrue();

        var cart = await _ctx.CartItems.CountAsync(ci =>
            _ctx.Orders.Any(o => o.Id == ci.OrderId && o.TicketId == ticket && o.Status == OrderStatus.Draft));
        cart.Should().Be(1);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task AcquireLock()
    {
        var r = await new AcquireTableLock.Handler(_ctx, Staff(), Clock(), Version(), Config())
            .Handle(new AcquireTableLock.Command(_tableId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
    }

    private async Task<long> OpenTicket()
    {
        var r = await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, null), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        return r.Value.TicketId;
    }

    private Task<Result<AddCartItem.Response>> AddItem(long ticketId, int itemId, decimal qty, string? notes = null)
    {
        return new AddCartItem.Handler(_ctx, Staff(), Clock(), Guard(), PriceResolver(), Rc(), Cart(), Version())
            .Handle(new AddCartItem.Command(ticketId, itemId, qty, notes, []), CancellationToken.None);
    }

    private Task<Result<SendOrder.Response>> Send(long ticketId)
    {
        return new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Rc(), Version())
            .Handle(new SendOrder.Command(ticketId, null), CancellationToken.None);
    }

    private ICurrentStaff Staff() => CreateStaff.Staff(_staffId);
    private IDateTimeProvider Clock() => CreateStaff.Clock();
    private ITableOperationGuard Guard() => new TableOperationGuard(_ctx, Clock(), Config());
    private IVersionService Version() => Substitute.For<IVersionService>();

    // Null config → typed accessors fall back to defaults (TTL = 60s).
    private IConfigValueService Config()
    {
        var c = Substitute.For<IConfigValueService>();
        c.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        return c;
    }
    private IRoundingConfig Rc() => CreateStaff.RoundingConfig();
    private ITicketRecomputeService TicketRecompute() => new TicketRecomputeService(_ctx, Rc(), Clock());
    private ICartRecomputeService Cart() => new CartRecomputeService(_ctx, Rc(), Clock());
    private IMenuPriceResolver PriceResolver() => new MenuPriceResolver(_ctx);

    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount { Username = "c", PasswordHash = "x", FullName = "Thu ngan", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var counter = new Counter { Name = "C", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area { Counter = counter, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 0m, ServiceChargeVatPercent = 0m, CreatedAt = now, UpdatedAt = now };
        var table = new Table { Area = area, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var shift = new Shift { Code = "S1", Name = "Sang", BeginTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59), IsActive = true, CreatedAt = now, UpdatedAt = now };
        var drawer = new CashDrawerSession { Counter = counter, Shift = shift, OpenedByStaffAccountId = 0, Status = CashDrawerStatus.Open, OpeningCash = 0m, OpenedAt = now, CreatedAt = now, UpdatedAt = now };

        var uomPhan = new Uom { Code = "phan", Name = "Phan", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var uomLon = new Uom { Code = "lon", Name = "Lon", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var uomLy = new Uom { Code = "ly", Name = "Ly", IsActive = true, CreatedAt = now, UpdatedAt = now };

        var pho = new Item { Code = "PHO", Name = "Pho", BaseUom = uomPhan, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var bia = new Item { Code = "BIA", Name = "Bia", BaseUom = uomLon, VatPercent = 10m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var coca = new Item { Code = "COCA", Name = "Coca", BaseUom = uomLon, VatPercent = 10m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var traDa = new Item { Code = "TRADA", Name = "Tra da", BaseUom = uomLy, VatPercent = 10m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var caPhe = new Item { Code = "CAPHE", Name = "Ca phe", BaseUom = uomLy, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var priceTable = new PriceTable { Code = "PT", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var variant = new PriceVariant { PriceTable = priceTable, Code = "PV", Name = "Base", AppliesToAllAreas = true, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var pePho = new PriceEntry { PriceVariant = variant, Item = pho, Price = 50_000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };
        var peBia = new PriceEntry { PriceVariant = variant, Item = bia, Price = 22_000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };
        var peCoca = new PriceEntry { PriceVariant = variant, Item = coca, Price = 15_000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };
        var peTraDa = new PriceEntry { PriceVariant = variant, Item = traDa, Price = 5_000m, IsVatIncluded = true, CreatedAt = now, UpdatedAt = now };
        var peCaPhe = new PriceEntry { PriceVariant = variant, Item = caPhe, Price = 20_000m, IsVatIncluded = true, CreatedAt = now, UpdatedAt = now };

        var dp1 = new DiscountPolicy { Code = "GIAM10", Name = "Bill 200k giam 10%", DiscountType = DiscountType.TicketThreshold, IsAutoApply = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        dp1.Conditions.Add(new DiscountPolicyCondition { DiscountPolicy = dp1, ThresholdAmount = 200_000m, ApplyType = DiscountApplyType.Percent, DiscountValue = 10m, DisplayOrder = 1, CreatedAt = now, UpdatedAt = now });
        dp1.Conditions.Add(new DiscountPolicyCondition { DiscountPolicy = dp1, ThresholdAmount = 500_000m, ApplyType = DiscountApplyType.Percent, DiscountValue = 15m, DisplayOrder = 2, CreatedAt = now, UpdatedAt = now });

        var dp2 = new DiscountPolicy { Code = "GIAM100", Name = "Bill 1M giam 100k", DiscountType = DiscountType.TicketThreshold, IsAutoApply = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        dp2.Conditions.Add(new DiscountPolicyCondition { DiscountPolicy = dp2, ThresholdAmount = 1_000_000m, ApplyType = DiscountApplyType.Fixed, DiscountValue = 100_000m, DisplayOrder = 1, CreatedAt = now, UpdatedAt = now });

        var cat = new Category { Code = "HANG_BAN", Name = "Hang ban", Level = 0, Path = "1;", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var catSub = new Category { Code = "DOUONG", Name = "Do uong", Parent = cat, Level = 1, Path = "1;999;", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };

        _ctx.AddRange(role, staff, counter, area, table, shift,
            uomPhan, uomLon, uomLy, pho, bia, coca, traDa, caPhe,
            priceTable, variant, pePho, peBia, peCoca, peTraDa, peCaPhe, dp1, dp2);
        await _ctx.SaveChangesAsync();
        cat.Path = $"1;{cat.Id};"; catSub.Path = $"1;{cat.Id};{catSub.Id};";
        await _ctx.SaveChangesAsync();

        _ctx.ItemCategories.AddRange(
            new ItemCategory { Item = pho, Category = catSub, IsMain = true, CreatedAt = now },
            new ItemCategory { Item = bia, Category = catSub, IsMain = true, CreatedAt = now },
            new ItemCategory { Item = coca, Category = catSub, IsMain = true, CreatedAt = now },
            new ItemCategory { Item = traDa, Category = catSub, IsMain = true, CreatedAt = now },
            new ItemCategory { Item = caPhe, Category = catSub, IsMain = true, CreatedAt = now }
        );
        _ctx.AreaMenuCategories.Add(new AreaMenuCategory { Area = area, Category = catSub, CreatedAt = now });

        drawer.OpenedByStaffAccountId = staff.Id; drawer.ShiftId = shift.Id;
        _ctx.AddRange(drawer);
        await _ctx.SaveChangesAsync();

        _staffId = staff.Id; _tableId = table.Id; _shiftId = shift.Id;
        _phoId = pho.Id; _biaId = bia.Id; _cocaId = coca.Id; _traDaId = traDa.Id; _caPheId = caPhe.Id;
    }
}

internal static class CreateStaff
{
    public static ICurrentStaff Staff(int id)
    {
        var s = Substitute.For<ICurrentStaff>();
        s.StaffAccountId.Returns(id);
        return s;
    }
    public static IDateTimeProvider Clock()
    {
        var c = Substitute.For<IDateTimeProvider>();
        c.UtcNow.Returns(new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc));
        return c;
    }
    public static IRoundingConfig RoundingConfig()
    {
        var rc = Substitute.For<IRoundingConfig>();
        rc.GetDigits(Arg.Any<string>()).Returns(2);
        return rc;
    }
}
