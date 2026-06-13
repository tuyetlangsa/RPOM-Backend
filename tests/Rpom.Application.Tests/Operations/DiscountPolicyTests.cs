using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.DiscountPolicies.CreateDiscountPolicy;
using Rpom.Application.DiscountPolicies.DeleteDiscountPolicy;
using Rpom.Application.DiscountPolicies.GetDiscountPolicy;
using Rpom.Application.DiscountPolicies.ListDiscountPolicies;
using Rpom.Application.DiscountPolicies.UpdateDiscountPolicy;
using Rpom.Domain.Access;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Operations;

/// <summary>Integration coverage for DiscountPolicy + Condition admin (B).</summary>
public sealed class DiscountPolicyTests : IAsyncLifetime
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

    [Fact]
    public async Task CreateTicketThreshold_ThenGetAndList()
    {
        var version = Substitute.For<IVersionService>();
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), version);
        var r = await create.Handle(new CreateDiscountPolicy.Command(
            "DC500", "Giảm bill 500k", null, DiscountType.TicketThreshold, false, null, true,
            new[]
            {
                new CreateDiscountPolicy.ConditionInput(500000m, null, null, null, DiscountApplyType.Fixed, 50000m, 1),
            }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.ConditionCount.Should().Be(1);
        await version.Received(1).BumpAsync(VersionScopes.Pricing, Arg.Any<string>(), Arg.Any<CancellationToken>());

        var g = await new GetDiscountPolicy.Handler(_ctx).Handle(new GetDiscountPolicy.Query(r.Value.Id), CancellationToken.None);
        g.Value.Conditions.Should().ContainSingle(c => c.ThresholdAmount == 500000m && c.ApplyType == DiscountApplyType.Fixed);

        var list = await new ListDiscountPolicies.Handler(_ctx)
            .Handle(new ListDiscountPolicies.Query(null, null, null), CancellationToken.None);
        list.Value.Should().ContainSingle(p => p.Code == "DC500" && p.ConditionCount == 1);
    }

    [Fact]
    public async Task CreateQuantityItem_UnknownItem_Fails()
    {
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var r = await create.Handle(new CreateDiscountPolicy.Command(
            "QI1", "Mua 3 combo", null, DiscountType.QuantityItem, false, null, true,
            new[]
            {
                new CreateDiscountPolicy.ConditionInput(null, 999999, 3m, null, DiscountApplyType.Percent, 10m, 1),
            }), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(DiscountPolicyErrors.ItemNotFound.Code);
    }

    [Fact]
    public async Task DuplicateCode_Conflicts()
    {
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var cmd = new CreateDiscountPolicy.Command(
            "DUP", "P", null, DiscountType.TicketThreshold, false, null, true,
            new[] { new CreateDiscountPolicy.ConditionInput(100000m, null, null, null, DiscountApplyType.Percent, 5m, 1) });
        await create.Handle(cmd, CancellationToken.None);
        var r2 = await create.Handle(cmd with { Name = "P2" }, CancellationToken.None);

        r2.IsFailure.Should().BeTrue();
        r2.Error.Code.Should().Be(DiscountPolicyErrors.CodeDuplicate.Code);
    }

    [Fact]
    public async Task Update_ReplacesConditions()
    {
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var r = await create.Handle(new CreateDiscountPolicy.Command(
            "UPD", "P", null, DiscountType.TicketThreshold, false, null, true,
            new[]
            {
                new CreateDiscountPolicy.ConditionInput(100000m, null, null, null, DiscountApplyType.Percent, 5m, 1),
                new CreateDiscountPolicy.ConditionInput(200000m, null, null, null, DiscountApplyType.Percent, 10m, 2),
            }), CancellationToken.None);

        var upd = new UpdateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var u = await upd.Handle(new UpdateDiscountPolicy.Command(
            r.Value.Id, "UPD", "P updated", null, DiscountType.TicketThreshold, true, "1,2,3", true,
            new[] { new UpdateDiscountPolicy.ConditionInput(300000m, null, null, null, DiscountApplyType.Fixed, 30000m, 1) }),
            CancellationToken.None);
        u.Value.ConditionCount.Should().Be(1);

        var g = await new GetDiscountPolicy.Handler(_ctx).Handle(new GetDiscountPolicy.Query(r.Value.Id), CancellationToken.None);
        g.Value.Conditions.Should().ContainSingle(c => c.ThresholdAmount == 300000m);
        g.Value.IsAutoApply.Should().BeTrue();
        g.Value.DaysOfWeek.Should().Be("1,2,3");
    }

    [Fact]
    public async Task Delete_WhenReferencedByTicket_Blocked()
    {
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var r = await create.Handle(new CreateDiscountPolicy.Command(
            "DEL", "P", null, DiscountType.TicketThreshold, false, null, true,
            new[] { new CreateDiscountPolicy.ConditionInput(100000m, null, null, null, DiscountApplyType.Percent, 5m, 1) }),
            CancellationToken.None);

        await SeedTicketReferencingPolicyAsync(r.Value.Id);

        var del = new DeleteDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var d = await del.Handle(new DeleteDiscountPolicy.Command(r.Value.Id), CancellationToken.None);

        d.IsFailure.Should().BeTrue();
        d.Error.Code.Should().Be(DiscountPolicyErrors.InUse.Code);
    }

    [Fact]
    public async Task Delete_WhenUnreferenced_Succeeds()
    {
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var r = await create.Handle(new CreateDiscountPolicy.Command(
            "DEL2", "P", null, DiscountType.TicketThreshold, false, null, true,
            new[] { new CreateDiscountPolicy.ConditionInput(100000m, null, null, null, DiscountApplyType.Percent, 5m, 1) }),
            CancellationToken.None);

        var del = new DeleteDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var d = await del.Handle(new DeleteDiscountPolicy.Command(r.Value.Id), CancellationToken.None);

        d.IsSuccess.Should().BeTrue();
        (await _ctx.DiscountPolicies.AnyAsync(p => p.Id == r.Value.Id)).Should().BeFalse();
        (await _ctx.DiscountPolicyConditions.AnyAsync(c => c.DiscountPolicyId == r.Value.Id)).Should().BeFalse();
    }

    [Fact] // QUANTITY_ITEM with a real item + area scope → success
    public async Task CreateQuantityItem_HappyPath_Succeeds()
    {
        var (itemId, areaId) = await SeedItemAndAreaAsync();
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());

        var r = await create.Handle(new CreateDiscountPolicy.Command(
            "QIOK", "Mua 3 combo giảm 10%", null, DiscountType.QuantityItem, false, null, true,
            new[]
            {
                new CreateDiscountPolicy.ConditionInput(null, itemId, 3m, areaId, DiscountApplyType.Percent, 10m, 1),
            }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var g = await new GetDiscountPolicy.Handler(_ctx).Handle(new GetDiscountPolicy.Query(r.Value.Id), CancellationToken.None);
        g.Value.Conditions.Should().ContainSingle(c => c.ItemId == itemId && c.AreaId == areaId && c.QuantityThreshold == 3m);
    }

    [Fact] // condition references a non-existent Area → AreaNotFound
    public async Task Create_UnknownArea_Fails()
    {
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var r = await create.Handle(new CreateDiscountPolicy.Command(
            "BADAREA", "P", null, DiscountType.TicketThreshold, false, null, true,
            new[] { new CreateDiscountPolicy.ConditionInput(100000m, null, null, 987654, DiscountApplyType.Percent, 5m, 1) }),
            CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(DiscountPolicyErrors.AreaNotFound.Code);
    }

    [Fact]
    public async Task Get_UnknownId_NotFound()
    {
        var g = await new GetDiscountPolicy.Handler(_ctx).Handle(new GetDiscountPolicy.Query(987654), CancellationToken.None);
        g.IsFailure.Should().BeTrue();
        g.Error.Code.Should().Be(DiscountPolicyErrors.NotFound.Code);
    }

    [Fact]
    public async Task Update_UnknownId_NotFound()
    {
        var upd = new UpdateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var u = await upd.Handle(new UpdateDiscountPolicy.Command(
            987654, "X", "X", null, DiscountType.TicketThreshold, false, null, true,
            new[] { new UpdateDiscountPolicy.ConditionInput(100000m, null, null, null, DiscountApplyType.Percent, 5m, 1) }),
            CancellationToken.None);

        u.IsFailure.Should().BeTrue();
        u.Error.Code.Should().Be(DiscountPolicyErrors.NotFound.Code);
    }

    [Fact] // updating policy B's code to policy A's code → conflict
    public async Task Update_DuplicateCode_Conflicts()
    {
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var cond = new[] { new CreateDiscountPolicy.ConditionInput(100000m, null, null, null, DiscountApplyType.Percent, 5m, 1) };
        await create.Handle(new CreateDiscountPolicy.Command("AAA", "A", null, DiscountType.TicketThreshold, false, null, true, cond), CancellationToken.None);
        var b = await create.Handle(new CreateDiscountPolicy.Command("BBB", "B", null, DiscountType.TicketThreshold, false, null, true, cond), CancellationToken.None);

        var upd = new UpdateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var u = await upd.Handle(new UpdateDiscountPolicy.Command(
            b.Value.Id, "AAA", "B", null, DiscountType.TicketThreshold, false, null, true,
            new[] { new UpdateDiscountPolicy.ConditionInput(100000m, null, null, null, DiscountApplyType.Percent, 5m, 1) }),
            CancellationToken.None);

        u.IsFailure.Should().BeTrue();
        u.Error.Code.Should().Be(DiscountPolicyErrors.CodeDuplicate.Code);
    }

    [Fact]
    public async Task List_FiltersByIsActiveAndType()
    {
        var create = new CreateDiscountPolicy.Handler(_ctx, Staff(), Clock(), Substitute.For<IVersionService>());
        var tcond = new[] { new CreateDiscountPolicy.ConditionInput(100000m, null, null, null, DiscountApplyType.Percent, 5m, 1) };
        await create.Handle(new CreateDiscountPolicy.Command("ACT", "Active", null, DiscountType.TicketThreshold, false, null, true, tcond), CancellationToken.None);
        await create.Handle(new CreateDiscountPolicy.Command("INA", "Inactive", null, DiscountType.TicketThreshold, false, null, false, tcond), CancellationToken.None);

        var list = new ListDiscountPolicies.Handler(_ctx);

        var actives = await list.Handle(new ListDiscountPolicies.Query(null, true, null), CancellationToken.None);
        actives.Value.Should().OnlyContain(p => p.IsActive);
        actives.Value.Should().Contain(p => p.Code == "ACT");

        var ticketType = await list.Handle(new ListDiscountPolicies.Query(null, null, DiscountType.TicketThreshold), CancellationToken.None);
        ticketType.Value.Should().OnlyContain(p => p.DiscountType == DiscountType.TicketThreshold);
        ticketType.Value.Should().Contain(p => p.Code == "ACT");

        // Filtering by the other type excludes the seeded TICKET_THRESHOLD policies.
        var quantityType = await list.Handle(new ListDiscountPolicies.Query(null, null, DiscountType.QuantityItem), CancellationToken.None);
        quantityType.Value.Should().NotContain(p => p.Code == "ACT" || p.Code == "INA");
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

    /// <summary>Seed one sellable Item + one Area; returns their ids (for QUANTITY_ITEM conditions).</summary>
    private async Task<(int itemId, int areaId)> SeedItemAndAreaAsync()
    {
        var now = DateTime.UtcNow;
        var counter = new Counter { Name = "CQ", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area
        {
            Counter = counter,
            Name = "AQ",
            DisplayOrder = 1,
            IsActive = true,
            ServiceChargePercent = 0m,
            ServiceChargeVatPercent = 0m,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var uom = new Uom { Code = "uQ", Name = "Cái", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var item = new Item
        {
            Code = "COMBO_Q",
            Name = "Combo Q",
            BaseUom = uom,
            VatPercent = 10m,
            IsStockable = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _ctx.AddRange(counter, area, uom, item);
        await _ctx.SaveChangesAsync();
        return (item.Id, area.Id);
    }

    /// <summary>Minimal Counter→Area→Table + CashDrawerSession + Shift + Ticket referencing the policy.</summary>
    private async Task SeedTicketReferencingPolicyAsync(int policyId)
    {
        var now = DateTime.UtcNow;
        var counter = new Counter { Name = "C", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area
        {
            Counter = counter,
            Name = "A",
            DisplayOrder = 1,
            IsActive = true,
            ServiceChargePercent = 0m,
            ServiceChargeVatPercent = 0m,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var table = new Table
        {
            Area = area,
            Code = "T01",
            SeatCount = 4,
            Status = TableStatus.Available,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var shift = new Shift { Code = "S1", Name = "Sáng", BeginTime = new TimeOnly(6, 0), EndTime = new TimeOnly(14, 0), IsActive = true, CreatedAt = now, UpdatedAt = now };
        var drawer = new Rpom.Domain.Sales.CashDrawer.CashDrawerSession
        {
            Counter = counter,
            Shift = shift,
            OpenedByStaffAccountId = _staffId,
            Status = "OPEN",
            OpeningCash = 0m,
            OpenedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _ctx.AddRange(counter, area, table, shift, drawer);
        await _ctx.SaveChangesAsync();

        var ticket = new Ticket
        {
            Code = "TK-1",
            TableId = table.Id,
            AreaId = area.Id,
            CounterId = counter.Id,
            CashDrawerSessionId = drawer.Id,
            ShiftId = shift.Id,
            GuestCount = 1,
            Status = TicketStatus.Open,
            OpenedAt = now,
            DiscountPolicyId = policyId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _ctx.Add(ticket);
        await _ctx.SaveChangesAsync();
    }
}
