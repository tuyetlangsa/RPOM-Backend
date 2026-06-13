using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Cashier.AcquireTableLock;
using Rpom.Application.Cashier.AddCartItem;
using Rpom.Application.Cashier.OpenTicket;
using Rpom.Domain.Access;
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

/// <summary>
///     AddCartItem merge rules: a note-free add folds into an existing identical note-free draft
///     line (quantity bumped) instead of inserting a duplicate. Singles match on item alone; set
///     menus must additionally match the full component/modifier selection (order-independent). Any
///     add carrying a line note is always a brand-new line.
/// </summary>
public sealed class AddCartItemMergeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId, _tableId;
    private int _singleItemId, _comboItemId, _riceId, _drinkCcId, _cokeId, _pepsiId;

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

    // ---------- single item ----------

    [Fact]
    public async Task SingleNoNote_SameItem_BumpsExistingLine()
    {
        var ticketId = await OpenTicketAsync();

        var a = await Add().Handle(
            new AddCartItem.Command(ticketId, _singleItemId, 1m, null, []), CancellationToken.None);
        var b = await Add().Handle(
            new AddCartItem.Command(ticketId, _singleItemId, 2m, null, []), CancellationToken.None);

        a.IsSuccess.Should().BeTrue();
        b.IsSuccess.Should().BeTrue();
        b.Value.CartItemId.Should().Be(a.Value.CartItemId); // same line returned

        var lines = await DraftLinesAsync(ticketId);
        lines.Should().ContainSingle();
        lines[0].Quantity.Should().Be(3m); // 1 + 2
    }

    [Fact]
    public async Task SingleWithNote_AlwaysSeparateLine()
    {
        var ticketId = await OpenTicketAsync();

        await Add().Handle(new AddCartItem.Command(ticketId, _singleItemId, 1m, "ít cay", []), CancellationToken.None);
        await Add().Handle(new AddCartItem.Command(ticketId, _singleItemId, 1m, "ít cay", []), CancellationToken.None);

        var lines = await DraftLinesAsync(ticketId);
        lines.Should().HaveCount(2);
        lines.Should().OnlyContain(l => l.Quantity == 1m);
    }

    [Fact]
    public async Task SingleNoNote_DoesNotMergeIntoNotedLine()
    {
        var ticketId = await OpenTicketAsync();

        await Add().Handle(new AddCartItem.Command(ticketId, _singleItemId, 1m, "ít cay", []), CancellationToken.None);
        await Add().Handle(new AddCartItem.Command(ticketId, _singleItemId, 1m, null, []), CancellationToken.None);

        var lines = await DraftLinesAsync(ticketId);
        lines.Should().HaveCount(2);
        lines.Should().Contain(l => l.Notes == "ít cay" && l.Quantity == 1m);
        lines.Should().Contain(l => l.Notes == null && l.Quantity == 1m);
    }

    // ---------- set menu ----------

    [Fact]
    public async Task SetMenuNoNote_SameSelection_MergesAndKeepsSingleDetailSet()
    {
        var ticketId = await OpenTicketAsync();

        var a = await Add().Handle(
            new AddCartItem.Command(ticketId, _comboItemId, 1m, null, ComboCoke()), CancellationToken.None);
        // Same selection, details submitted in reversed order → must still merge (order-independent).
        var b = await Add().Handle(
            new AddCartItem.Command(ticketId, _comboItemId, 1m, null, ComboCokeReversed()), CancellationToken.None);

        a.IsSuccess.Should().BeTrue();
        b.IsSuccess.Should().BeTrue();
        b.Value.CartItemId.Should().Be(a.Value.CartItemId);

        var lines = await DraftLinesAsync(ticketId);
        lines.Should().ContainSingle();
        lines[0].Quantity.Should().Be(2m);

        // Details belong to the single merged line and were NOT duplicated.
        var detailCount = await _ctx.CartItemDetails.CountAsync(d => d.CartItemId == lines[0].Id);
        detailCount.Should().Be(2);
    }

    [Fact]
    public async Task SetMenuNoNote_DifferentSelection_NewLine()
    {
        var ticketId = await OpenTicketAsync();

        await Add().Handle(new AddCartItem.Command(ticketId, _comboItemId, 1m, null, ComboCoke()), CancellationToken.None);
        await Add().Handle(new AddCartItem.Command(ticketId, _comboItemId, 1m, null, ComboPepsi()), CancellationToken.None);

        var lines = await DraftLinesAsync(ticketId);
        lines.Should().HaveCount(2);
        lines.Should().OnlyContain(l => l.Quantity == 1m);
    }

    [Fact]
    public async Task SetMenuWithNote_AlwaysSeparateLine()
    {
        var ticketId = await OpenTicketAsync();

        await Add().Handle(new AddCartItem.Command(ticketId, _comboItemId, 1m, "không hành", ComboCoke()), CancellationToken.None);
        await Add().Handle(new AddCartItem.Command(ticketId, _comboItemId, 1m, "không hành", ComboCoke()), CancellationToken.None);

        var lines = await DraftLinesAsync(ticketId);
        lines.Should().HaveCount(2);
    }

    // ---------- helpers ----------

    private List<AddCartItem.DetailInput> ComboCoke() =>
    [
        new(null, _riceId, ComponentType.MainComponent, 1m, null),
        new(_drinkCcId, _cokeId, ComponentType.Modifier, 1m, null),
    ];

    private List<AddCartItem.DetailInput> ComboCokeReversed() =>
    [
        new(_drinkCcId, _cokeId, ComponentType.Modifier, 1m, null),
        new(null, _riceId, ComponentType.MainComponent, 1m, null),
    ];

    private List<AddCartItem.DetailInput> ComboPepsi() =>
    [
        new(null, _riceId, ComponentType.MainComponent, 1m, null),
        new(_drinkCcId, _pepsiId, ComponentType.Modifier, 1m, null),
    ];

    private async Task<List<CartItem>> DraftLinesAsync(long ticketId) =>
        await _ctx.CartItems
            .Where(c => _ctx.Orders.Any(o =>
                o.Id == c.OrderId && o.TicketId == ticketId && o.Status == OrderStatus.Draft))
            .ToListAsync();

    private async Task<long> OpenTicketAsync()
    {
        await new AcquireTableLock.Handler(_ctx, Staff(), Clock(), Version(), Config())
            .Handle(new AcquireTableLock.Command(_tableId), CancellationToken.None);
        var open = await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, null), CancellationToken.None);
        open.IsSuccess.Should().BeTrue();
        return open.Value.TicketId;
    }

    private AddCartItem.Handler Add() => new(
        _ctx, Staff(), Clock(), Guard(), new MenuPriceResolver(_ctx), Rc(), Cart(), Version());

    private TableOperationGuard Guard() => new(_ctx, Clock(), Config());
    private CartRecomputeService Cart() => new(_ctx, Rc(), Clock());
    private static IVersionService Version() => Substitute.For<IVersionService>();

    // Null config → typed accessors fall back to defaults (TTL = 60s).
    private static Rpom.Application.Abstraction.Configuration.IConfigValueService Config()
    {
        var c = Substitute.For<Rpom.Application.Abstraction.Configuration.IConfigValueService>();
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

    private IRoundingConfig Rc()
    {
        var rc = Substitute.For<IRoundingConfig>();
        foreach (var kv in RoundingKeys.Defaults)
        {
            rc.GetDigits(kv.Key).Returns(kv.Value);
        }

        return rc;
    }

    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount { Username = "c", PasswordHash = "x", FullName = "Thu ngân", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var counter = new Counter { Name = "C", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area { Counter = counter, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var table = new Table { Area = area, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var shift = new Shift { Code = "S1", Name = "Sáng", BeginTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59), IsActive = true, CreatedAt = now, UpdatedAt = now };
        var drawer = new CashDrawerSession { Counter = counter, OpenedByStaffAccountId = 0, Status = CashDrawerStatus.Open, OpeningCash = 0m, OpenedAt = now, CreatedAt = now, UpdatedAt = now };

        var uom = new Uom { Code = "phan", Name = "Phần", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var single = new Item { Code = "PHO", Name = "Phở", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var combo = new Item { Code = "COMBO", Name = "Combo trưa", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var rice = new Item { Code = "RICE", Name = "Cơm", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var coke = new Item { Code = "COKE", Name = "Coca", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var pepsi = new Item { Code = "PEPSI", Name = "Pepsi", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var priceTable = new PriceTable { Code = "PT", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var variant = new PriceVariant { PriceTable = priceTable, Code = "PV", Name = "Base", AppliesToAllAreas = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var entrySingle = new PriceEntry { PriceVariant = variant, Item = single, Price = 50000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };
        var entryCombo = new PriceEntry { PriceVariant = variant, Item = combo, Price = 100000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };

        var drinkCc = new ChoiceCategory { Name = "Đồ uống", MinChoice = 1, MaxChoice = 1, DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var modCoke = new Modifier { ChoiceCategory = drinkCc, Item = coke, ExtraPrice = 0m, MinPerModifier = 0, MaxPerModifier = 1, DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var modPepsi = new Modifier { ChoiceCategory = drinkCc, Item = pepsi, ExtraPrice = 5000m, MinPerModifier = 0, MaxPerModifier = 1, DisplayOrder = 2, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var setMenu = new SetMenu { Item = combo, CreatedAt = now, UpdatedAt = now };

        _ctx.AddRange(role, staff, counter, area, table, shift, uom,
            single, combo, rice, coke, pepsi, priceTable, variant, entrySingle, entryCombo,
            drinkCc, modCoke, modPepsi, setMenu);
        await _ctx.SaveChangesAsync();

        // Set menu details reference the combo Item id + component/choice rows now that ids exist.
        _ctx.AddRange(
            new SetMenuDetail { SetMenuItemId = combo.Id, DetailType = SetMenuDetailType.Component, ComponentItemId = rice.Id, Quantity = 1m, IsFixed = true, DisplayOrder = 1, CreatedAt = now, UpdatedAt = now },
            new SetMenuDetail { SetMenuItemId = combo.Id, DetailType = SetMenuDetailType.ChoiceCategory, ChoiceCategoryId = drinkCc.Id, DisplayOrder = 2, CreatedAt = now, UpdatedAt = now });

        drawer.OpenedByStaffAccountId = staff.Id;
        drawer.ShiftId = shift.Id;
        _ctx.Add(drawer);
        await _ctx.SaveChangesAsync();

        _staffId = staff.Id; _tableId = table.Id;
        _singleItemId = single.Id; _comboItemId = combo.Id; _riceId = rice.Id;
        _drinkCcId = drinkCc.Id; _cokeId = coke.Id; _pepsiId = pepsi.Id;
    }
}
