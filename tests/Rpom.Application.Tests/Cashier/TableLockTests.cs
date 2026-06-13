using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Cashier.AcquireTableLock;
using Rpom.Application.Cashier.ReleaseTableLock;
using Rpom.Domain.Access;
using Rpom.Domain.Restaurant;
using Rpom.Infrastructure.Database;
using Rpom.Infrastructure.Tables;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Cashier;

/// <summary>Integration coverage for the W0 table operation lock (acquire/release/guard).</summary>
public sealed class TableLockTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffA;
    private int _staffB;
    private int _tableId;

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
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Acquire_OnFreeTable_Succeeds_AndBumpsFloorPlan()
    {
        var version = Substitute.For<IVersionService>();
        var r = await Acquire(_staffA, version);

        r.IsSuccess.Should().BeTrue();
        r.Value.StaffAccountId.Should().Be(_staffA);
        await version.Received(1).BumpAsync(VersionScopes.FloorPlan, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Acquire_Mine_IsHeartbeat_NoBump()
    {
        await Acquire(_staffA);
        var version = Substitute.For<IVersionService>();
        var r = await Acquire(_staffA, version);

        r.IsSuccess.Should().BeTrue();
        await version.DidNotReceive().BumpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Acquire_HeldByOther_Live_Blocked()
    {
        await Acquire(_staffA);
        var r = await Acquire(_staffB);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("TableLock.HeldByOther");
    }

    [Fact]
    public async Task Acquire_HeldByOther_Stale_TakenOver()
    {
        await Acquire(_staffA);
        // Age staff A's lock beyond TTL.
        var row = await _ctx.TableLocks.FirstAsync(l => l.TableId == _tableId);
        row.LastHeartbeatAt = DateTime.UtcNow.AddSeconds(-(ITableOperationGuard.DefaultTtlSeconds + 5));
        await _ctx.SaveChangesAsync();

        var r = await Acquire(_staffB);
        r.IsSuccess.Should().BeTrue();
        r.Value.StaffAccountId.Should().Be(_staffB);
    }

    [Fact]
    public async Task Release_Mine_RemovesLock()
    {
        await Acquire(_staffA);
        var rel = new ReleaseTableLock.Handler(_ctx, Staff(_staffA), Substitute.For<IVersionService>());
        var r = await rel.Handle(new ReleaseTableLock.Command(_tableId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        (await _ctx.TableLocks.AnyAsync(l => l.TableId == _tableId)).Should().BeFalse();
    }

    [Fact]
    public async Task Release_NotMine_NoOp()
    {
        await Acquire(_staffA);
        var rel = new ReleaseTableLock.Handler(_ctx, Staff(_staffB), Substitute.For<IVersionService>());
        var r = await rel.Handle(new ReleaseTableLock.Command(_tableId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        (await _ctx.TableLocks.AnyAsync(l => l.TableId == _tableId)).Should().BeTrue(); // A still holds it
    }

    [Fact]
    public async Task Guard_RejectsNonHolder_AcceptsHolder()
    {
        await Acquire(_staffA);
        var guard = new TableOperationGuard(_ctx, Clock(), Config());

        (await guard.EnsureHeldAsync(_tableId, _staffB, CancellationToken.None)).IsFailure.Should().BeTrue();

        var held = await guard.EnsureHeldAsync(_tableId, _staffA, CancellationToken.None);
        held.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Guard_RejectsStaleLock()
    {
        await Acquire(_staffA);
        var row = await _ctx.TableLocks.FirstAsync(l => l.TableId == _tableId);
        row.LastHeartbeatAt = DateTime.UtcNow.AddSeconds(-(ITableOperationGuard.DefaultTtlSeconds + 5));
        await _ctx.SaveChangesAsync();

        var guard = new TableOperationGuard(_ctx, Clock(), Config());
        (await guard.EnsureHeldAsync(_tableId, _staffA, CancellationToken.None)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Guard_HonorsConfiguredTtl()
    {
        await Acquire(_staffA);
        // Age the lock to 90s — beyond a configured 30s TTL but within the 60s default.
        var row = await _ctx.TableLocks.FirstAsync(l => l.TableId == _tableId);
        row.LastHeartbeatAt = DateTime.UtcNow.AddSeconds(-90);
        await _ctx.SaveChangesAsync();

        var config = Substitute.For<Rpom.Application.Abstraction.Configuration.IConfigValueService>();
        config.GetAsync("table.lock_ttl_seconds", Arg.Any<CancellationToken>()).Returns("30");
        var guard = new TableOperationGuard(_ctx, Clock(), config);

        (await guard.EnsureHeldAsync(_tableId, _staffA, CancellationToken.None)).IsFailure.Should().BeTrue();
    }

    // ---------- helpers ----------

    private Task<Rpom.Domain.Common.Result<AcquireTableLock.Response>> Acquire(
        int staffId, IVersionService? version = null)
        => new AcquireTableLock.Handler(
                _ctx, Staff(staffId), Clock(),
                version ?? Substitute.For<IVersionService>(), Config())
            .Handle(new AcquireTableLock.Command(_tableId), CancellationToken.None);

    private ICurrentStaff Staff(int staffId)
    {
        var s = Substitute.For<ICurrentStaff>();
        s.StaffAccountId.Returns(staffId);
        return s;
    }

    private static IDateTimeProvider Clock()
    {
        var c = Substitute.For<IDateTimeProvider>();
        c.UtcNow.Returns(_ => DateTime.UtcNow);
        return c;
    }

    // Returns null for every code → GetIntAsync falls back to DefaultTtlSeconds (60).
    private static Rpom.Application.Abstraction.Configuration.IConfigValueService Config()
    {
        var c = Substitute.For<Rpom.Application.Abstraction.Configuration.IConfigValueService>();
        c.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        return c;
    }

    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var a = new StaffAccount { Username = "a", PasswordHash = "x", FullName = "Nhân viên A", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var b = new StaffAccount { Username = "b", PasswordHash = "x", FullName = "Nhân viên B", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var counter = new Counter { Name = "C", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area { Counter = counter, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 0m, ServiceChargeVatPercent = 0m, CreatedAt = now, UpdatedAt = now };
        var table = new Table { Area = area, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        _ctx.AddRange(role, a, b, counter, area, table);
        await _ctx.SaveChangesAsync();
        _staffA = a.Id; _staffB = b.Id; _tableId = table.Id;
    }
}
