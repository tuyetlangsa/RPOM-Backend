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
using Rpom.Application.Cashier.ApplyDiscountPolicy;
using Rpom.Application.Cashier.OpenTicket;
using Rpom.Application.Cashier.RemoveDiscount;
using Rpom.Application.Cashier.SendOrder;
using Rpom.Domain.Access;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales.CashDrawer;
using Rpom.Infrastructure.Database;
using Rpom.Infrastructure.Pricing;
using Rpom.Infrastructure.Tables;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Cashier;

public sealed class DiscountIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId, _tableId, _shiftId, _item1Id, _item2Id, _policyId, _fixedPolicyId;

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
    public async Task Apply_TicketThreshold_Percent_SetsDiscount()
    {
        var ticketId = await OpenAndSendAsync();

        var apply = await new ApplyDiscountPolicy.Handler(
            _ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new ApplyDiscountPolicy.Command(ticketId, _policyId), CancellationToken.None);
        apply.IsSuccess.Should().BeTrue();
        apply.Value.DiscountAmount.Should().BeGreaterThan(0);

        var ticket = await _ctx.Tickets.FirstAsync(t => t.Id == ticketId);
        ticket.DiscountPolicyId.Should().Be(_policyId);
        ticket.DiscountPercent.Should().Be(10m);
    }

    [Fact]
    public async Task Apply_PolicyNotActive_NotFound()
    {
        var ticketId = await OpenAndSendAsync();

        // Create a policy then deactivate it.
        var now = DateTime.UtcNow;
        var policy = new DiscountPolicy
        {
            Code = "INACTIVE",
            Name = "Inactive",
            DiscountType = DiscountType.TicketThreshold,
            IsAutoApply = false,
            IsActive = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _ctx.DiscountPolicies.Add(policy);
        await _ctx.SaveChangesAsync();

        var apply = await new ApplyDiscountPolicy.Handler(
            _ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new ApplyDiscountPolicy.Command(ticketId, policy.Id), CancellationToken.None);
        apply.IsFailure.Should().BeTrue();
        apply.Error.Code.Should().Be("Discount.PolicyNotFound");
    }

    [Fact]
    public async Task Apply_AlreadyApplied_Conflict()
    {
        var ticketId = await OpenAndSendAsync();

        await new ApplyDiscountPolicy.Handler(
            _ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new ApplyDiscountPolicy.Command(ticketId, _policyId), CancellationToken.None);

        var apply2 = await new ApplyDiscountPolicy.Handler(
            _ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new ApplyDiscountPolicy.Command(ticketId, _policyId), CancellationToken.None);
        apply2.IsFailure.Should().BeTrue();
        apply2.Error.Code.Should().Be("Discount.AlreadyApplied");
    }

    [Fact]
    public async Task Apply_ThenRemove_ClearsDiscount()
    {
        var ticketId = await OpenAndSendAsync();

        await new ApplyDiscountPolicy.Handler(
            _ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new ApplyDiscountPolicy.Command(ticketId, _policyId), CancellationToken.None);

        var rem = await new RemoveDiscount.Handler(
            _ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new RemoveDiscount.Command(ticketId), CancellationToken.None);
        rem.IsSuccess.Should().BeTrue();

        var ticket = await _ctx.Tickets.FirstAsync(t => t.Id == ticketId);
        ticket.DiscountPolicyId.Should().BeNull();
        ticket.DiscountPercent.Should().Be(0m);
        ticket.DiscountAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Remove_NoDiscount_Idempotent()
    {
        var ticketId = await OpenAndSendAsync();

        var rem = await new RemoveDiscount.Handler(
            _ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new RemoveDiscount.Command(ticketId), CancellationToken.None);
        rem.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Apply_FixedAmount_DistributesExactly()
    {
        var ticketId = await OpenAndSendAsync(quantity1: 2m, quantity2: 1m); // 2 Phở + 1 Bún

        var apply = await new ApplyDiscountPolicy.Handler(
            _ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new ApplyDiscountPolicy.Command(ticketId, _fixedPolicyId), CancellationToken.None);
        apply.IsSuccess.Should().BeTrue();
        apply.Value.DiscountAmount.Should().Be(5_000m);

        // Sum of TicketDiscountAmount across all items must equal 5,000 exactly.
        var items = await _ctx.OrderItems.Where(o => o.TicketId == ticketId).ToListAsync();
        items.Sum(o => o.TicketDiscountAmount).Should().Be(5_000m);
        items.Sum(o => o.LineSubtotal).Should().BeGreaterThan(0);
    }

    // ---- helpers ----

    /// <summary>Open a ticket, add item(s), send order → returns ticketId with an applied discount-ready state.</summary>
    private async Task<long> OpenAndSendAsync(decimal quantity1 = 1m, decimal quantity2 = 0m)
    {
        await AcquireLock();
        var ticketId = (await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, null), CancellationToken.None)).Value.TicketId;

        await new AddCartItem.Handler(_ctx, Staff(), Clock(), Guard(), new MenuPriceResolver(_ctx), Rc(), Cart(), Version())
            .Handle(new AddCartItem.Command(ticketId, _item1Id, quantity1, null, []), CancellationToken.None);

        if (quantity2 > 0)
        {
            await new AddCartItem.Handler(_ctx, Staff(), Clock(), Guard(), new MenuPriceResolver(_ctx), Rc(), Cart(), Version())
                .Handle(new AddCartItem.Command(ticketId, _item2Id, quantity2, null, []), CancellationToken.None);
        }

        await new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Rc(), Version())
            .Handle(new SendOrder.Command(ticketId, null), CancellationToken.None);

        return ticketId;
    }

    private async Task AcquireLock()
    {
        await new AcquireTableLock.Handler(_ctx, Staff(), Clock(), Version(), Config())
            .Handle(new AcquireTableLock.Command(_tableId), CancellationToken.None);
    }

    private TableOperationGuard Guard() => new(_ctx, Clock(), Config());
    private CartRecomputeService Cart() => new(_ctx, Rc(), Clock());
    private TicketRecomputeService TicketRecompute() => new(_ctx, Rc(), Clock());
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
        c.UtcNow.Returns(DateTime.UtcNow);
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
        var item1 = new Item { Code = "PHO", Name = "Phở", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var item2 = new Item { Code = "BUN", Name = "Bún", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var priceTable = new PriceTable { Code = "PT", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var variant = new PriceVariant { PriceTable = priceTable, Code = "PV", Name = "Base", AppliesToAllAreas = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var entry1 = new PriceEntry { PriceVariant = variant, Item = item1, Price = 50_000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };
        var entry2 = new PriceEntry { PriceVariant = variant, Item = item2, Price = 40_000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };

        // TICKET_THRESHOLD PERCENT policy: subtotal ≥ 10k → 10%
        var policy = new DiscountPolicy
        {
            Code = "TICKET10",
            Name = "Bill trên 10k giảm 10%",
            DiscountType = DiscountType.TicketThreshold,
            IsAutoApply = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var condition = new DiscountPolicyCondition
        {
            DiscountPolicy = policy,
            ThresholdAmount = 10_000m,
            ApplyType = DiscountApplyType.Percent,
            DiscountValue = 10m,
            DisplayOrder = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // TICKET_THRESHOLD FIXED policy: subtotal ≥ 10k → 5,000đ
        var fixedPolicy = new DiscountPolicy
        {
            Code = "FIXED5K",
            Name = "Giảm thẳng 5k",
            DiscountType = DiscountType.TicketThreshold,
            IsAutoApply = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var fixedCondition = new DiscountPolicyCondition
        {
            DiscountPolicy = fixedPolicy,
            ThresholdAmount = 10_000m,
            ApplyType = DiscountApplyType.Fixed,
            DiscountValue = 5_000m,
            DisplayOrder = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _ctx.AddRange(role, staff, counter, area, table, shift, uom, item1, item2,
            priceTable, variant, entry1, entry2, policy, condition, fixedPolicy, fixedCondition);
        await _ctx.SaveChangesAsync();

        drawer.OpenedByStaffAccountId = staff.Id;
        drawer.ShiftId = shift.Id;
        _ctx.Add(drawer);
        await _ctx.SaveChangesAsync();

        _staffId = staff.Id; _tableId = table.Id; _shiftId = shift.Id;
        _item1Id = item1.Id; _item2Id = item2.Id;
        _policyId = policy.Id; _fixedPolicyId = fixedPolicy.Id;
    }
}
