using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.ChoiceCategories.CreateChoiceCategory;
using Rpom.Application.ChoiceCategories.DeleteChoiceCategory;
using Rpom.Application.ChoiceCategories.GetChoiceCategory;
using Rpom.Application.ChoiceCategories.SetModifiers;
using Rpom.Application.SetMenus.DeleteSetMenu;
using Rpom.Application.SetMenus.GetSetMenu;
using Rpom.Application.SetMenus.UpsertSetMenu;
using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Menu;

/// <summary>
/// Integration coverage for ChoiceCategory + Modifier (C2) and SetMenu (C3) admin
/// against a real Postgres container.
/// </summary>
public sealed class ChoiceCategorySetMenuTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                _db.GetConnectionString(),
                o => o.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default).UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        _ctx = new ApplicationDbContext(options);
        await _ctx.Database.MigrateAsync();
        _staffId = await SeedStaffAsync();
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    // ---------- C2: ChoiceCategory + Modifier ----------

    [Fact]
    public async Task CreateChoiceCategory_ThenGet_Roundtrips()
    {
        var create = new CreateChoiceCategory.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var r = await create.Handle(
            new CreateChoiceCategory.Command("Topping", "ghi chú", 1, 3, 1, true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var get = new GetChoiceCategory.Handler(_ctx);
        var g = await get.Handle(new GetChoiceCategory.Query(r.Value.Id), CancellationToken.None);
        g.Value.Name.Should().Be("Topping");
        g.Value.MaxChoice.Should().Be(3);
        g.Value.Modifiers.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateChoiceCategory_DuplicateName_Conflicts()
    {
        var create = new CreateChoiceCategory.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        await create.Handle(new CreateChoiceCategory.Command("Sauce", null, 1, null, 1, true), CancellationToken.None);
        var r2 = await create.Handle(new CreateChoiceCategory.Command("sauce", null, 1, null, 1, true), CancellationToken.None);

        r2.IsFailure.Should().BeTrue();
        r2.Error.Code.Should().Be(ChoiceCategoryErrors.NameDuplicate.Code);
    }

    [Fact]
    public async Task SetModifiers_ReplaceAll_InsertsUpdatesDeletes()
    {
        var ccId = await SeedChoiceCategoryAsync("CC1");
        var beef = await SeedItemAsync("BEEF", "Thêm bò");
        var egg = await SeedItemAsync("EGG", "Thêm trứng");
        var version = Substitute.For<IVersionService>();
        var set = new SetModifiers.Handler(_ctx, Staff(), Clock(), version);

        var r1 = await set.Handle(new SetModifiers.Command(ccId, new[]
        {
            new SetModifiers.ModifierInput(beef, 20000m, 0, 1, 1, true),
            new SetModifiers.ModifierInput(egg, 5000m, 0, 2, 2, true),
        }), CancellationToken.None);
        r1.Value.Inserted.Should().Be(2);
        await version.Received(1).BumpAsync(VersionScopes.Menu, Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Replace: update beef price, drop egg.
        var r2 = await set.Handle(new SetModifiers.Command(ccId, new[]
        {
            new SetModifiers.ModifierInput(beef, 25000m, 0, 1, 1, true),
        }), CancellationToken.None);
        r2.Value.Updated.Should().Be(1);
        r2.Value.Deleted.Should().Be(1);

        var get = new GetChoiceCategory.Handler(_ctx);
        var g = await get.Handle(new GetChoiceCategory.Query(ccId), CancellationToken.None);
        g.Value.Modifiers.Should().ContainSingle(m => m.ItemId == beef && m.ExtraPrice == 25000m);
    }

    [Fact]
    public async Task SetModifiers_UnknownItem_Fails()
    {
        var ccId = await SeedChoiceCategoryAsync("CC2");
        var set = new SetModifiers.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());

        var r = await set.Handle(new SetModifiers.Command(ccId, new[]
        {
            new SetModifiers.ModifierInput(999999, 1000m, 0, 1, 1, true),
        }), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ChoiceCategoryErrors.ItemNotFound.Code);
    }

    [Fact]
    public async Task DeleteChoiceCategory_WhenUsedBySetMenu_Blocked()
    {
        var ccId = await SeedChoiceCategoryAsync("CC3");
        var setItem = await SeedItemAsync("SET1", "Combo");
        await Upsert(setItem, new[]
        {
            new UpsertSetMenu.DetailInput(SetMenuDetailType.ChoiceCategory, null, null, null, ccId, 1),
        });

        var del = new DeleteChoiceCategory.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var r = await del.Handle(new DeleteChoiceCategory.Command(ccId), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ChoiceCategoryErrors.InUse.Code);
    }

    // ---------- C3: SetMenu ----------

    [Fact]
    public async Task UpsertSetMenu_MakesItemSetMenu_AndGetMenuSurfacesIt()
    {
        var setItem = await SeedItemAsync("COMBO", "Combo Phở");
        var beef = await SeedItemAsync("PHO", "Phở bò");
        var ccId = await SeedChoiceCategoryAsync("Nước");

        var r = await Upsert(setItem, new[]
        {
            new UpsertSetMenu.DetailInput(SetMenuDetailType.Component, beef, 1m, true, null, 1),
            new UpsertSetMenu.DetailInput(SetMenuDetailType.ChoiceCategory, null, null, null, ccId, 2),
        });
        r.IsSuccess.Should().BeTrue();
        r.Value.DetailCount.Should().Be(2);

        var get = new GetSetMenu.Handler(_ctx);
        var g = await get.Handle(new GetSetMenu.Query(setItem), CancellationToken.None);
        g.Value.Details.Should().HaveCount(2);
        g.Value.Details.Should().Contain(d => d.DetailType == SetMenuDetailType.Component && d.ComponentItemId == beef);

        var isSet = await _ctx.SetMenus.AnyAsync(s => s.ItemId == setItem);
        isSet.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertSetMenu_SelfComponent_Fails()
    {
        var setItem = await SeedItemAsync("SELF", "Tự combo");
        var r = await Upsert(setItem, new[]
        {
            new UpsertSetMenu.DetailInput(SetMenuDetailType.Component, setItem, 1m, true, null, 1),
        });

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(SetMenuErrors.SelfComponent.Code);
    }

    [Fact]
    public async Task UpsertSetMenu_UnknownChoiceCategory_Fails()
    {
        var setItem = await SeedItemAsync("COMBO2", "Combo 2");
        var r = await Upsert(setItem, new[]
        {
            new UpsertSetMenu.DetailInput(SetMenuDetailType.ChoiceCategory, null, null, null, 987654, 1),
        });

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(SetMenuErrors.ChoiceCategoryNotFound.Code);
    }

    [Fact]
    public async Task DeleteSetMenu_RevertsToSingle()
    {
        var setItem = await SeedItemAsync("COMBO3", "Combo 3");
        var comp = await SeedItemAsync("SIDE", "Món phụ");
        await Upsert(setItem, new[]
        {
            new UpsertSetMenu.DetailInput(SetMenuDetailType.Component, comp, 1m, true, null, 1),
        });

        var del = new DeleteSetMenu.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var r = await del.Handle(new DeleteSetMenu.Command(setItem), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();

        (await _ctx.SetMenus.AnyAsync(s => s.ItemId == setItem)).Should().BeFalse();
        (await _ctx.SetMenuDetails.AnyAsync(d => d.SetMenuItemId == setItem)).Should().BeFalse();
    }

    // ---------- helpers ----------

    private ICurrentStaff Staff()
    {
        var s = Substitute.For<ICurrentStaff>();
        s.StaffAccountId.Returns(_staffId);
        return s;
    }

    private static IDateTimeProvider Clock()
    {
        var c = Substitute.For<IDateTimeProvider>();
        c.UtcNow.Returns(DateTime.UtcNow);
        return c;
    }

    private Task<Result<UpsertSetMenu.Response>> Upsert(int itemId, IReadOnlyList<UpsertSetMenu.DetailInput> details)
    {
        var handler = new UpsertSetMenu.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        return handler.Handle(new UpsertSetMenu.Command(itemId, null, details), CancellationToken.None);
    }

    private async Task<int> SeedStaffAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "OWNER", Name = "Owner", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount
        {
            Username = "owner",
            PasswordHash = "x",
            FullName = "Owner",
            Role = role,
            IsActive = true,
            IsLocked = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _ctx.AddRange(role, staff);
        await _ctx.SaveChangesAsync();
        return staff.Id;
    }

    private async Task<int> SeedChoiceCategoryAsync(string name)
    {
        var now = DateTime.UtcNow;
        var cc = new ChoiceCategory
        {
            Name = name,
            MinChoice = 1,
            MaxChoice = null,
            DisplayOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _ctx.Add(cc);
        await _ctx.SaveChangesAsync();
        return cc.Id;
    }

    private async Task<int> SeedItemAsync(string code, string name)
    {
        var now = DateTime.UtcNow;
        var uom = new Uom { Code = $"u{code}", Name = "Cái", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var item = new Item
        {
            Code = code,
            Name = name,
            BaseUom = uom,
            VatPercent = 10m,
            IsStockable = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _ctx.AddRange(uom, item);
        await _ctx.SaveChangesAsync();
        return item.Id;
    }
}
