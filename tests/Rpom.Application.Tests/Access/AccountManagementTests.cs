using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Access;
using Rpom.Application.Access.ListRoles;
using Rpom.Application.Access.CreateStaffAccount;
using Rpom.Application.Access.GetStaffAccount;
using Rpom.Application.Access.ListStaffAccounts;
using Rpom.Domain.Access;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Access;

public sealed class AccountManagementTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _cashierRoleId;
    private int _ownerStaffId;
    private int _cashierStaffId;

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
        var ownerRole = new Role { Code = Roles.Owner, Name = "Owner", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var cashierRole = new Role { Code = Roles.Cashier, Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var owner = new StaffAccount { Username = "owner", PasswordHash = "x", FullName = "Owner", Role = ownerRole, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var cashier = new StaffAccount { Username = "cashier01", PasswordHash = "x", FullName = "Nguyen Van A", Phone = "0901", Role = cashierRole, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };

        var grp = new PermissionGroup { Code = "pos", Name = "POS", DisplayOrder = 1 };
        var permOpen = new Permission { Code = Permissions.TicketOpen, Name = "Open ticket", PermissionGroup = grp, DisplayOrder = 1 };
        var permClose = new Permission { Code = Permissions.TicketClose, Name = "Close ticket", PermissionGroup = grp, DisplayOrder = 2 };

        _ctx.AddRange(ownerRole, cashierRole, owner, cashier, grp, permOpen, permClose);
        await _ctx.SaveChangesAsync();

        _cashierRoleId = cashierRole.Id;
        _ownerStaffId = owner.Id;
        _cashierStaffId = cashier.Id;
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task ListRoles_ReturnsRolesWithAccountCounts()
    {
        var handler = new ListRoles.Handler(_ctx);

        var result = await handler.Handle(new ListRoles.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().HaveCount(2);
        result.Value.Roles.Single(r => r.Code == Roles.Cashier).AccountCount.Should().Be(1);
        result.Value.Roles.Single(r => r.Code == Roles.Owner).AccountCount.Should().Be(1);
    }

    [Fact]
    public async Task ListStaffAccounts_FilterByRole_ReturnsOnlyThatRole()
    {
        var handler = new ListStaffAccounts.Handler(_ctx);

        var result = await handler.Handle(
            new ListStaffAccounts.Query(_cashierRoleId, null, 1, 50), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.Username.Should().Be("cashier01");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task ListStaffAccounts_Search_MatchesUsernameOrFullName()
    {
        var handler = new ListStaffAccounts.Handler(_ctx);

        var result = await handler.Handle(
            new ListStaffAccounts.Query(null, "nguyen", 1, 50), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.Username.Should().Be("cashier01");
    }

    [Fact]
    public async Task ListStaffAccounts_NoFilter_ReturnsAll()
    {
        var handler = new ListStaffAccounts.Handler(_ctx);

        var result = await handler.Handle(
            new ListStaffAccounts.Query(null, null, 1, 50), CancellationToken.None);

        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetStaffAccount_ReturnsDetail()
    {
        var handler = new GetStaffAccount.Handler(_ctx);

        var result = await handler.Handle(
            new GetStaffAccount.Query(_cashierStaffId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Username.Should().Be("cashier01");
        result.Value.RoleCode.Should().Be(Roles.Cashier);
    }

    [Fact]
    public async Task GetStaffAccount_UnknownId_NotFound()
    {
        var handler = new GetStaffAccount.Handler(_ctx);

        var result = await handler.Handle(
            new GetStaffAccount.Query(999999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Access.StaffNotFound");
    }

    [Fact]
    public async Task CreateStaffAccount_Succeeds_HashesPassword()
    {
        var handler = new CreateStaffAccount.Handler(_ctx, Staff(), Clock(), Version(), Hasher());

        var result = await handler.Handle(
            new CreateStaffAccount.Command("newuser", "secret1", "New User", "0911", "n@x.vn", _cashierRoleId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var created = await _ctx.StaffAccounts.FirstAsync(x => x.Username == "newuser");
        created.PasswordHash.Should().Be("HASH:secret1");
        created.RoleId.Should().Be(_cashierRoleId);
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateStaffAccount_DuplicateUsername_Fails()
    {
        var handler = new CreateStaffAccount.Handler(_ctx, Staff(), Clock(), Version(), Hasher());

        var result = await handler.Handle(
            new CreateStaffAccount.Command("cashier01", "secret1", "Dup", null, null, _cashierRoleId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Access.UsernameDuplicate");
    }

    [Fact]
    public async Task CreateStaffAccount_UnknownRole_Fails()
    {
        var handler = new CreateStaffAccount.Handler(_ctx, Staff(), Clock(), Version(), Hasher());

        var result = await handler.Handle(
            new CreateStaffAccount.Command("u2", "secret1", "U2", null, null, 999999),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Access.RoleNotFound");
    }

    // ---- shared test helpers (used by later tasks) ----

    private ICurrentStaff Staff() => Staff(_ownerStaffId);

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

    private static IPasswordHasher Hasher()
    {
        var h = Substitute.For<IPasswordHasher>();
        h.Hash(Arg.Any<string>()).Returns(ci => "HASH:" + ci.Arg<string>());
        return h;
    }
}
