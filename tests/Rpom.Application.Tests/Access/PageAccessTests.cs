using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Access;
using Rpom.Application.Access.GetMyMenu;
using Rpom.Application.Access.GetStaffPageAccess;
using Rpom.Application.Access.GetRolePageDefault;
using Rpom.Application.Access.SetStaffPageAccess;
using Rpom.Domain.Access;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Access;

public sealed class PageAccessTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _cashierStaffId;
    private int _emptyStaffId;

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

        DateTime now = DateTime.UtcNow;
        var role = new Role { Code = Roles.Cashier, Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var cashierModule = new Module { Code = Modules.Cashier, Name = "Cashier", DisplayOrder = 20 };
        var kitchenModule = new Module { Code = Modules.Kitchen, Name = "Kitchen", DisplayOrder = 40 };
        var pTickets = new Page { Code = Pages.CashierTickets, Name = "Tickets", Module = cashierModule, DisplayOrder = 1 };
        var pPayment = new Page { Code = Pages.CashierPayment, Name = "Payment", Module = cashierModule, DisplayOrder = 2 };
        var pKds = new Page { Code = Pages.KitchenKds, Name = "Kitchen Display", Module = kitchenModule, DisplayOrder = 1 };

        var cashier = new StaffAccount { Username = "c", PasswordHash = "x", FullName = "Cashier", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var empty = new StaffAccount { Username = "e", PasswordHash = "x", FullName = "Empty", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };

        _ctx.AddRange(role, cashierModule, kitchenModule, pTickets, pPayment, pKds, cashier, empty);
        await _ctx.SaveChangesAsync();

        // Cashier granted only the two cashier pages (NOT the kitchen page).
        _ctx.StaffAccountPageAccesses.Add(new StaffAccountPageAccess { StaffAccountId = cashier.Id, PageId = pTickets.Id, CreatedAt = now });
        _ctx.StaffAccountPageAccesses.Add(new StaffAccountPageAccess { StaffAccountId = cashier.Id, PageId = pPayment.Id, CreatedAt = now });
        await _ctx.SaveChangesAsync();

        _cashierStaffId = cashier.Id;
        _emptyStaffId = empty.Id;
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetMyMenu_ReturnsOnlyGrantedModulesAndPages()
    {
        var handler = new GetMyMenu.Handler(_ctx, Staff(_cashierStaffId));

        var result = await handler.Handle(new GetMyMenu.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Modules.Should().ContainSingle();
        GetMyMenu.ModuleNode module = result.Value.Modules[0];
        module.Code.Should().Be(Modules.Cashier);
        module.Pages.Select(p => p.Code).Should().BeEquivalentTo(new[] { Pages.CashierTickets, Pages.CashierPayment });
    }

    [Fact]
    public async Task GetMyMenu_EmptyAccount_ReturnsNoModules()
    {
        var handler = new GetMyMenu.Handler(_ctx, Staff(_emptyStaffId));

        var result = await handler.Handle(new GetMyMenu.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Modules.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStaffPageAccess_ReturnsFullCatalogWithGrantedFlags()
    {
        var handler = new GetStaffPageAccess.Handler(_ctx);

        var result = await handler.Handle(
            new GetStaffPageAccess.Query(_cashierStaffId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // Full catalog: both modules present, all pages listed.
        result.Value.Modules.Should().HaveCount(2);
        var allPages = result.Value.Modules.SelectMany(m => m.Pages).ToList();
        allPages.Should().HaveCount(3);

        // Granted flags reflect the cashier's two grants only.
        allPages.Single(p => p.Code == Pages.CashierTickets).Granted.Should().BeTrue();
        allPages.Single(p => p.Code == Pages.CashierPayment).Granted.Should().BeTrue();
        allPages.Single(p => p.Code == Pages.KitchenKds).Granted.Should().BeFalse();
    }

    [Fact]
    public async Task GetStaffPageAccess_UnknownAccount_NotFound()
    {
        var handler = new GetStaffPageAccess.Handler(_ctx);

        var result = await handler.Handle(
            new GetStaffPageAccess.Query(999999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Access.StaffNotFound");
    }

    [Fact]
    public async Task SetStaffPageAccess_FullReplace_AddsAndRemoves()
    {
        var handler = new SetStaffPageAccess.Handler(_ctx, Staff(_cashierStaffId), Clock(), Version());

        // Cashier currently has {tickets, payment}. Replace with {payment, kds}.
        var result = await handler.Handle(
            new SetStaffPageAccess.Command(
                _cashierStaffId,
                new[] { Pages.CashierPayment, Pages.KitchenKds }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.GrantedPageCodes.Should().BeEquivalentTo(new[] { Pages.CashierPayment, Pages.KitchenKds });

        var persisted = await _ctx.StaffAccountPageAccesses
            .Where(x => x.StaffAccountId == _cashierStaffId)
            .Select(x => x.Page.Code)
            .ToListAsync();
        persisted.Should().BeEquivalentTo(new[] { Pages.CashierPayment, Pages.KitchenKds });
    }

    [Fact]
    public async Task SetStaffPageAccess_UnknownPageCode_Fails()
    {
        var handler = new SetStaffPageAccess.Handler(_ctx, Staff(_cashierStaffId), Clock(), Version());

        var result = await handler.Handle(
            new SetStaffPageAccess.Command(_cashierStaffId, new[] { "does.not.exist" }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Access.UnknownPageCode");
    }

    [Fact]
    public async Task SetStaffPageAccess_UnknownAccount_NotFound()
    {
        var handler = new SetStaffPageAccess.Handler(_ctx, Staff(_cashierStaffId), Clock(), Version());

        var result = await handler.Handle(
            new SetStaffPageAccess.Command(999999, new[] { Pages.CashierPayment }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Access.StaffNotFound");
    }

    [Fact]
    public async Task SetStaffPageAccess_BumpsAccessVersion()
    {
        var version = Version();
        var handler = new SetStaffPageAccess.Handler(_ctx, Staff(_cashierStaffId), Clock(), version);

        await handler.Handle(
            new SetStaffPageAccess.Command(_cashierStaffId, new[] { Pages.CashierPayment }),
            CancellationToken.None);

        await version.Received(1).BumpAsync(
            VersionScopes.Access, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRolePageDefault_Cashier_ReturnsCashierTemplate()
    {
        var handler = new GetRolePageDefault.Handler();

        var result = await handler.Handle(
            new GetRolePageDefault.Query(Roles.Cashier), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageCodes.Should().BeEquivalentTo(new[]
        {
            Pages.CashierFloorPlan, Pages.CashierTickets, Pages.CashierPayment, Pages.CashierCashDrawer
        });
    }

    [Fact]
    public async Task GetRolePageDefault_UnknownRole_ReturnsEmpty()
    {
        var handler = new GetRolePageDefault.Handler();

        var result = await handler.Handle(
            new GetRolePageDefault.Query("NO_SUCH_ROLE"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageCodes.Should().BeEmpty();
    }

    private static ICurrentStaff Staff(int id)
    {
        var s = Substitute.For<ICurrentStaff>();
        s.StaffAccountId.Returns(id);
        return s;
    }

    private static IDateTimeProvider Clock()
    {
        var c = Substitute.For<IDateTimeProvider>();
        c.UtcNow.Returns(_ => DateTime.UtcNow);
        return c;
    }

    private static IVersionService Version()
    {
        var v = Substitute.For<IVersionService>();
        v.BumpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return v;
    }
}
