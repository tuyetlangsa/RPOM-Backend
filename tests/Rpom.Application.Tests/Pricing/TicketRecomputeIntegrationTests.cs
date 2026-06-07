using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Domain.Access;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;
using Rpom.Infrastructure.Database;
using Rpom.Infrastructure.Pricing;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Pricing;

/// <summary>
/// End-to-end smoke for <see cref="TicketRecomputeService"/> against a real Postgres
/// container: seed a minimal valid Ticket graph → recompute → assert header rollup
/// + TicketItemSum buckets. The pure pricing math is covered by PricingCalculatorTests;
/// this verifies service wiring (load → recompute lines → rebuild buckets → roll up header).
/// </summary>
public sealed class TicketRecomputeIntegrationTests : IAsyncLifetime
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

    private static IRoundingConfig DefaultRc()
    {
        var rc = Substitute.For<IRoundingConfig>();
        foreach (var kv in RoundingKeys.Defaults)
        {
            rc.GetDigits(kv.Key).Returns(kv.Value);
        }

        return rc;
    }

    [Fact]
    public async Task Recompute_RollsUpTicketHeader_FromOrderItems()
    {
        // Arrange
        var ticketId = await SeedTicketWithTwoLinesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc));

        var svc = new TicketRecomputeService(_ctx, DefaultRc(), clock);

        // Act
        await svc.RecomputeAsync(ticketId, CancellationToken.None);
        await _ctx.SaveChangesAsync();

        // Assert — header rollup (pricing spec §6: 2 Bia @50000 VAT10 + 1 Phở @80000 VAT8, SC 10% scVat 10%)
        var ticket = await _ctx.Tickets.AsNoTracking().FirstAsync(t => t.Id == ticketId);
        ticket.Subtotal.Should().Be(180000m);
        ticket.ServiceChargeAmount.Should().Be(18000m);
        ticket.VatAmount.Should().Be(18200m);
        ticket.TotalAmount.Should().Be(216200m);

        var buckets = await _ctx.TicketItemSums.AsNoTracking()
            .Where(s => s.TicketId == ticketId).ToListAsync();
        buckets.Should().HaveCount(2);
    }

    /// <summary>
    /// Seeds the minimal NOT NULL graph via EF entity objects (EF assigns FKs and
    /// identity PKs, orders inserts). Counter→Area→Table, Shift, Role→StaffAccount→
    /// CashDrawerSession, Uom→Item, then Ticket (SC 10%, scVat 10%, OPEN) with an
    /// Order holding 2 PENDING OrderItems. Returns the ticket id.
    /// </summary>
    private async Task<long> SeedTicketWithTwoLinesAsync()
    {
        var now = new DateTime(2026, 6, 6, 7, 0, 0, DateTimeKind.Utc);

        var counter = new Counter { Name = "Counter 1", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area
        {
            Counter = counter,
            Name = "Area A",
            DisplayOrder = 1,
            IsActive = true,
            ServiceChargePercent = 10m,
            ServiceChargeVatPercent = 10m,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var table = new Table { Area = area, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var shift = new Shift
        {
            Code = "S_MORNING",
            Name = "Morning",
            BeginTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(14, 0),
            IsNextDay = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount
        {
            Username = "cashier1",
            PasswordHash = "x",
            FullName = "Cashier One",
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var drawer = new CashDrawerSession
        {
            Counter = counter,
            OpenedByStaff = staff,
            OpenedAt = now,
            OpeningCash = 0m,
            Status = CashDrawerStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var uom = new Uom { Code = "lon", Name = "Lon", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var bia = new Item { Code = "BIA01", Name = "Bia", BaseUom = uom, VatPercent = 10m, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var pho = new Item { Code = "PHO01", Name = "Pho", BaseUom = uom, VatPercent = 8m, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var ticket = new Ticket
        {
            Code = "T-2026-0001",
            Table = table,
            Area = area,
            Counter = counter,
            CashDrawerSession = drawer,
            Shift = shift,
            GuestCount = 1,
            Status = TicketStatus.Open,
            OpenedAt = now,
            ServiceChargePercent = 10m,
            ServiceChargeVatPercent = 10m,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var order = new Order
        {
            Ticket = ticket,
            OrderNumber = 1,
            Status = OrderStatus.Sent,
            SentAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var biaLine = new OrderItem
        {
            Order = order,
            Ticket = ticket,
            Item = bia,
            ItemCode = "BIA01",
            ItemName = "Bia",
            Uom = uom,
            UomCode = "lon",
            UomName = "Lon",
            Quantity = 2m,
            UnitPrice = 50000m,
            VatPercent = 10m,
            Status = OrderItemStatus.Pending,
            SentAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var phoLine = new OrderItem
        {
            Order = order,
            Ticket = ticket,
            Item = pho,
            ItemCode = "PHO01",
            ItemName = "Pho",
            Uom = uom,
            UomCode = "to",
            UomName = "To",
            Quantity = 1m,
            UnitPrice = 80000m,
            VatPercent = 8m,
            Status = OrderItemStatus.Pending,
            SentAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _ctx.AddRange(counter, area, table, shift, role, staff, drawer, uom, bia, pho, ticket, order, biaLine, phoLine);
        await _ctx.SaveChangesAsync();

        return ticket.Id;
    }
}
