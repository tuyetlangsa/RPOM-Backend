using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Areas.GetAreaMenuCategories;
using Rpom.Application.Areas.SetAreaMenuCategories;
using Rpom.Application.Cashier.GetMenu;
using Rpom.Domain.Access;
using Rpom.Domain.Menu;
using Rpom.Domain.Restaurant;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Areas;

/// <summary>
/// Integration coverage for the Area↔Menu changes against a real Postgres container:
/// (1) GetMenu subtree rollup — parent category surfaces descendant items + correct
/// itemCount; (2) AreaMenuCategory admin replace-all GET/PUT semantics.
/// </summary>
public sealed class AreaMenuCategoryTests : IAsyncLifetime
{
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

    [Fact] // Rollup: area assigned PARENT, item in CHILD → item.categoryIds has both;
           // parent.itemCount counts the descendant item.
    public async Task GetMenu_ParentAssigned_RollsUpChildItems()
    {
        var seed = await SeedTreeWithItemInChildAsync(assignToArea: AssignTarget.Parent);

        var rc = Substitute.For<IRoundingConfig>();
        foreach (var kv in RoundingKeys.Defaults) rc.GetDigits(kv.Key).Returns(kv.Value);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);

        var handler = new GetMenu.Handler(_ctx, clock, rc);
        var result = await handler.Handle(new GetMenu.Query(seed.TableId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var item = result.Value.Items.Single(i => i.ItemId == seed.ItemId);
        item.CategoryIds.Should().Contain(seed.ParentId);
        item.CategoryIds.Should().Contain(seed.ChildId);

        var parentCat = result.Value.Categories.Single(c => c.CategoryId == seed.ParentId);
        parentCat.ItemCount.Should().Be(1);
        var childCat = result.Value.Categories.Single(c => c.CategoryId == seed.ChildId);
        childCat.ItemCount.Should().Be(1);
    }

    [Fact] // Child assigned directly (parent NOT visible) → categoryIds intersect = visible only.
    public async Task GetMenu_ChildAssigned_ExcludesNonVisibleParent()
    {
        var seed = await SeedTreeWithItemInChildAsync(assignToArea: AssignTarget.Child);

        var rc = Substitute.For<IRoundingConfig>();
        foreach (var kv in RoundingKeys.Defaults) rc.GetDigits(kv.Key).Returns(kv.Value);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);

        var handler = new GetMenu.Handler(_ctx, clock, rc);
        var result = await handler.Handle(new GetMenu.Query(seed.TableId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Categories.Should().NotContain(c => c.CategoryId == seed.ParentId);

        var item = result.Value.Items.Single(i => i.ItemId == seed.ItemId);
        item.CategoryIds.Should().Contain(seed.ChildId);
        item.CategoryIds.Should().NotContain(seed.ParentId);
    }

    [Fact] // PUT set → GET returns; replace removes old; empty clears.
    public async Task SetAndGet_ReplaceAll_Roundtrips()
    {
        var seed = await SeedTreeWithItemInChildAsync(assignToArea: AssignTarget.None);
        var version = Substitute.For<IVersionService>();
        var set = NewSetHandler(seed.StaffId, version);
        var get = new GetAreaMenuCategories.Handler(_ctx);

        // Assign parent.
        var r1 = await set.Handle(
            new SetAreaMenuCategories.Command(seed.AreaId, new[] { seed.ParentId }), CancellationToken.None);
        r1.IsSuccess.Should().BeTrue();
        r1.Value.Inserted.Should().Be(1);
        await version.Received(1).BumpAsync(VersionScopes.Menu, Arg.Any<string>(), Arg.Any<CancellationToken>());

        var g1 = await get.Handle(new GetAreaMenuCategories.Query(seed.AreaId), CancellationToken.None);
        g1.Value.Categories.Select(c => c.CategoryId).Should().BeEquivalentTo(new[] { seed.ParentId });

        // Replace parent → child.
        var r2 = await set.Handle(
            new SetAreaMenuCategories.Command(seed.AreaId, new[] { seed.ChildId }), CancellationToken.None);
        r2.Value.Inserted.Should().Be(1);
        r2.Value.Deleted.Should().Be(1);

        var g2 = await get.Handle(new GetAreaMenuCategories.Query(seed.AreaId), CancellationToken.None);
        g2.Value.Categories.Select(c => c.CategoryId).Should().BeEquivalentTo(new[] { seed.ChildId });

        // Empty clears.
        var r3 = await set.Handle(
            new SetAreaMenuCategories.Command(seed.AreaId, Array.Empty<int>()), CancellationToken.None);
        r3.Value.Deleted.Should().Be(1);
        r3.Value.Total.Should().Be(0);

        var g3 = await get.Handle(new GetAreaMenuCategories.Query(seed.AreaId), CancellationToken.None);
        g3.Value.Categories.Should().BeEmpty();
    }

    [Fact] // No-op replace (payload == current) → no version bump.
    public async Task Set_NoChange_DoesNotBumpVersion()
    {
        var seed = await SeedTreeWithItemInChildAsync(assignToArea: AssignTarget.Parent);
        var version = Substitute.For<IVersionService>();
        var set = NewSetHandler(seed.StaffId, version);

        var r = await set.Handle(
            new SetAreaMenuCategories.Command(seed.AreaId, new[] { seed.ParentId }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Inserted.Should().Be(0);
        r.Value.Deleted.Should().Be(0);
        await version.DidNotReceive().BumpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact] // Invalid category id → CategoryErrors.NotFound.
    public async Task Set_InvalidCategory_Fails()
    {
        var seed = await SeedTreeWithItemInChildAsync(assignToArea: AssignTarget.None);
        var set = NewSetHandler(seed.StaffId, Substitute.For<IVersionService>());

        var r = await set.Handle(
            new SetAreaMenuCategories.Command(seed.AreaId, new[] { 999999 }), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(CategoryErrors.NotFound.Code);
    }

    [Fact] // Unknown area → AreaErrors.NotFound.
    public async Task Set_UnknownArea_Fails()
    {
        var seed = await SeedTreeWithItemInChildAsync(assignToArea: AssignTarget.None);
        var set = NewSetHandler(seed.StaffId, Substitute.For<IVersionService>());

        var r = await set.Handle(
            new SetAreaMenuCategories.Command(987654, new[] { seed.ParentId }), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(AreaErrors.NotFound.Code);
    }

    private SetAreaMenuCategories.Handler NewSetHandler(int staffId, IVersionService version)
    {
        var currentStaff = Substitute.For<ICurrentStaff>();
        currentStaff.StaffAccountId.Returns(staffId);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        return new SetAreaMenuCategories.Handler(_ctx, currentStaff, clock, version);
    }

    private enum AssignTarget { None, Parent, Child }

    private sealed record SeedResult(
        int TableId, int AreaId, int ParentId, int ChildId, int ItemId, int StaffId);

    /// <summary>
    /// Counter→Area→Table + Category tree (parent→child) + Item linked to CHILD only +
    /// active PriceTable→Variant→Entry + a StaffAccount (for audit). Optionally seeds
    /// AreaMenuCategory targeting the parent or child.
    /// </summary>
    private async Task<SeedResult> SeedTreeWithItemInChildAsync(AssignTarget assignToArea)
    {
        var now = DateTime.UtcNow;

        var counter = new Counter { Name = "C", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area
        {
            Counter = counter, Name = "A", DisplayOrder = 1, IsActive = true,
            ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now,
        };
        var table = new Table
        {
            Area = area, Code = "T01", SeatCount = 4, Status = TableStatus.Available,
            IsActive = true, CreatedAt = now, UpdatedAt = now,
        };

        var parent = new Category
        {
            Code = "DRINK", Name = "Đồ uống", ParentId = null, Path = "PLACEHOLDER",
            Level = 0, DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        _ctx.Add(parent);
        await _ctx.SaveChangesAsync();
        parent.Path = $"{parent.Id};";

        var child = new Category
        {
            Code = "BEER", Name = "Bia", ParentId = parent.Id, Path = "PLACEHOLDER",
            Level = 1, DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        _ctx.Add(child);
        await _ctx.SaveChangesAsync();
        child.Path = $"{parent.Id};{child.Id};";
        await _ctx.SaveChangesAsync();

        var uom = new Uom { Code = "lon", Name = "Lon", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var item = new Item
        {
            Code = "BIA01", Name = "Bia Tiger", BaseUom = uom, VatPercent = 10m,
            IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var itemCategory = new ItemCategory { Item = item, Category = child, IsMain = true, CreatedAt = now };

        var priceTable = new PriceTable { Code = "PT", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var variant = new PriceVariant
        {
            PriceTable = priceTable, Code = "PV", Name = "Base",
            BeginTime = null, EndTime = null, DayMask = null, AppliesToAllAreas = true,
            IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var entry = new PriceEntry
        {
            PriceVariant = variant, Item = item, Price = 50000m, IsVatIncluded = false,
            CreatedAt = now, UpdatedAt = now,
        };

        var role = new Role { Code = "OWNER", Name = "Owner", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount
        {
            Username = "owner", PasswordHash = "x", FullName = "Owner", Role = role,
            IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now,
        };

        _ctx.AddRange(counter, area, table, uom, item, itemCategory, priceTable, variant, entry, role, staff);

        if (assignToArea == AssignTarget.Parent)
            _ctx.Add(new AreaMenuCategory { Area = area, Category = parent, CreatedAt = now });
        else if (assignToArea == AssignTarget.Child)
            _ctx.Add(new AreaMenuCategory { Area = area, Category = child, CreatedAt = now });

        await _ctx.SaveChangesAsync();

        return new SeedResult(table.Id, area.Id, parent.Id, child.Id, item.Id, staff.Id);
    }
}
