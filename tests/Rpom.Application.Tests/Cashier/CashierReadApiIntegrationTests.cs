using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Cashier.GetFloorPlan;
using Rpom.Application.Cashier.GetMenu;
using Rpom.Domain.Menu;
using Rpom.Domain.Restaurant;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Cashier;

/// <summary>
/// End-to-end smoke for the cashier read handlers against a real Postgres container.
/// Verifies query wiring (load → project → resolve) for GetFloorPlan and GetMenu by
/// seeding the minimal valid NOT NULL graph via EF entities and asserting the response.
/// </summary>
public sealed class CashierReadApiIntegrationTests : IAsyncLifetime
{
    // pgvector image — the schema declares a vector(1536) column (RagDocumentChunk.Embedding),
    // so the migration runs CREATE EXTENSION vector, unavailable in the base postgres image.
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                _db.GetConnectionString(),
                npgsqlOptions => npgsqlOptions
                    .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default)
                    .UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        _ctx = new ApplicationDbContext(options);
        await _ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact] // TC-F1 + TC-T2: counter with areas/tables, no tickets → all AVAILABLE, empty briefs
    public async Task FloorPlan_NoTickets_AllAvailable()
    {
        var counterId = await SeedCounterWithAreaAndTableAsync();

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        var config = Substitute.For<IConfigValueService>();
        config.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("30"));

        var handler = new GetFloorPlan.Handler(_ctx, clock, config);
        var result = await handler.Handle(new GetFloorPlan.Query(counterId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Areas.Should().NotBeEmpty();
        result.Value.Areas[0].Tables.Should().NotBeEmpty();
        result.Value.Areas[0].Tables.Should().OnlyContain(t => t.Status == "AVAILABLE");
        result.Value.Areas[0].Tables.Should().OnlyContain(t => t.LatestTicket == null);
    }

    [Fact] // TC-M8: area service-charge percent surfaced on the menu response
    public async Task Menu_SurfacesAreaServiceCharge()
    {
        var (tableId, areaScPercent) = await SeedTableWithMenuAsync();

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);

        var rc = Substitute.For<IRoundingConfig>();
        foreach (var kv in RoundingKeys.Defaults)
            rc.GetDigits(kv.Key).Returns(kv.Value);

        var handler = new GetMenu.Handler(_ctx, clock, rc);
        var result = await handler.Handle(new GetMenu.Query(tableId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ServiceChargePercent.Should().Be(areaScPercent);
    }

    [Fact] // unpriced items are silently hidden; only the priced one appears
    public async Task Menu_HidesItemsWithoutPrice()
    {
        var tableId = await SeedTableWithPricedAndUnpricedItemsAsync();

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        var rc = Substitute.For<IRoundingConfig>();
        foreach (var kv in RoundingKeys.Defaults) rc.GetDigits(kv.Key).Returns(kv.Value);

        var result = await new GetMenu.Handler(_ctx, clock, rc)
            .Handle(new GetMenu.Query(tableId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Code == "PRICED");
        result.Value.Items.Should().NotContain(i => i.Code == "NOPRICE");
        result.Value.Items.Should().OnlyContain(i => i.BasePrice != null && i.DisplayPrice != null);
    }

    /// <summary>One priced + one unpriced item in the same area category. Returns the table id.</summary>
    private async Task<int> SeedTableWithPricedAndUnpricedItemsAsync()
    {
        var now = DateTime.UtcNow;
        var counter = new Counter { Name = "Counter H", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area
        {
            Counter = counter, Name = "Area H", DisplayOrder = 1, IsActive = true,
            ServiceChargePercent = 0m, ServiceChargeVatPercent = 0m, CreatedAt = now, UpdatedAt = now,
        };
        var table = new Table
        {
            Area = area, Code = "TH01", SeatCount = 4, Status = TableStatus.Available,
            IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var category = new Category
        {
            Code = "CATH", Name = "Cat H", ParentId = null, Path = "PLACEHOLDER",
            Level = 0, DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        _ctx.Add(category);
        await _ctx.SaveChangesAsync();
        category.Path = $"{category.Id};";

        var amc = new AreaMenuCategory { Area = area, Category = category, CreatedAt = now };
        var uom = new Uom { Code = "cai", Name = "Cái", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var priced = new Item { Code = "PRICED", Name = "Có giá", BaseUom = uom, VatPercent = 10m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var unpriced = new Item { Code = "NOPRICE", Name = "Không giá", BaseUom = uom, VatPercent = 10m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var icP = new ItemCategory { Item = priced, Category = category, IsMain = true, CreatedAt = now };
        var icU = new ItemCategory { Item = unpriced, Category = category, IsMain = true, CreatedAt = now };

        var priceTable = new PriceTable { Code = "PTH", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var variant = new PriceVariant
        {
            PriceTable = priceTable, Code = "PVH", Name = "Base",
            AppliesToAllAreas = true, IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var entry = new PriceEntry { PriceVariant = variant, Item = priced, Price = 50000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };

        _ctx.AddRange(counter, area, table, amc, uom, priced, unpriced, icP, icU, priceTable, variant, entry);
        await _ctx.SaveChangesAsync();
        return table.Id;
    }

    /// <summary>Counter→Area→Table only (no tickets). Returns the counter id.</summary>
    private async Task<int> SeedCounterWithAreaAndTableAsync()
    {
        var now = DateTime.UtcNow;

        var counter = new Counter { Name = "Counter 1", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area
        {
            Counter = counter, Name = "Area A", DisplayOrder = 1, IsActive = true,
            ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now,
        };
        var table = new Table
        {
            Area = area, Code = "T01", SeatCount = 4, Status = TableStatus.Available,
            IsActive = true, CreatedAt = now, UpdatedAt = now,
        };

        _ctx.AddRange(counter, area, table);
        await _ctx.SaveChangesAsync();

        return counter.Id;
    }

    /// <summary>
    /// Counter→Area(SC%)→Table + active PriceTable→PriceVariant→PriceEntry +
    /// Uom→Item + Category→AreaMenuCategory→ItemCategory so the menu resolves a priced item.
    /// Returns the table id and the area's service-charge percent.
    /// </summary>
    private async Task<(int tableId, decimal areaScPercent)> SeedTableWithMenuAsync()
    {
        var now = DateTime.UtcNow;
        const decimal areaScPercent = 10m;

        var counter = new Counter { Name = "Counter M", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area
        {
            Counter = counter, Name = "Area M", DisplayOrder = 1, IsActive = true,
            ServiceChargePercent = areaScPercent, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now,
        };
        var table = new Table
        {
            Area = area, Code = "TM01", SeatCount = 4, Status = TableStatus.Available,
            IsActive = true, CreatedAt = now, UpdatedAt = now,
        };

        // Menu catalog: category visible to the area, one priced item linked to it.
        // Path follows the real convention (numeric id, semicolon-terminated) — set
        // after save so GetMenu's ancestor-id parsing sees realistic data.
        var category = new Category
        {
            Code = "CAT01", Name = "Drinks", ParentId = null, Path = "PLACEHOLDER",
            Level = 0, DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        _ctx.Add(category);
        await _ctx.SaveChangesAsync();
        category.Path = $"{category.Id};";

        var areaMenuCategory = new AreaMenuCategory { Area = area, Category = category, CreatedAt = now };

        var uom = new Uom { Code = "lon", Name = "Lon", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var item = new Item
        {
            Code = "BIA01", Name = "Bia", BaseUom = uom, VatPercent = 10m,
            IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var itemCategory = new ItemCategory { Item = item, Category = category, IsMain = true, CreatedAt = now };

        // Active price table → all-areas variant → entry for the item.
        var priceTable = new PriceTable
        {
            Code = "PT01", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var variant = new PriceVariant
        {
            PriceTable = priceTable, Code = "PV01", Name = "Base",
            BeginTime = null, EndTime = null, DayMask = null, AppliesToAllAreas = true,
            IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var entry = new PriceEntry
        {
            PriceVariant = variant, Item = item, Price = 50000m, IsVatIncluded = false,
            CreatedAt = now, UpdatedAt = now,
        };

        _ctx.AddRange(counter, area, table, areaMenuCategory, uom, item, itemCategory,
            priceTable, variant, entry);
        await _ctx.SaveChangesAsync();

        return (table.Id, areaScPercent);
    }
}
