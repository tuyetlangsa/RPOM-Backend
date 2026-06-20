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
