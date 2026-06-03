using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Domain.Access;
using Rpom.Domain.Ai;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Inventory;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.HasDefaultSchema(Schemas.Default);
    }

    // --- Area A: Access (RBAC) ---
    public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();
    public DbSet<StaffAccountPermission> StaffAccountPermissions => Set<StaffAccountPermission>();

    // --- Area B: Restaurant ---
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Table> Tables => Set<Table>();

    // --- Area C: Menu & Catalog ---
    public DbSet<Uom> Uoms => Set<Uom>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ChoiceCategory> ChoiceCategories => Set<ChoiceCategory>();
    public DbSet<PriceTable> PriceTables => Set<PriceTable>();
    public DbSet<PriceVariant> PriceVariants => Set<PriceVariant>();
    public DbSet<PriceEntry> PriceEntries => Set<PriceEntry>();
    public DbSet<SetMenu> SetMenus => Set<SetMenu>();
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<SetMenuDetail> SetMenuDetails => Set<SetMenuDetail>();
    public DbSet<Modifier> Modifiers => Set<Modifier>();
    public DbSet<AreaMenuCategory> AreaMenuCategories => Set<AreaMenuCategory>();
    public DbSet<PriceVariantArea> PriceVariantAreas => Set<PriceVariantArea>();

    // --- Area D: Operations Config ---
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<KitchenStation> KitchenStations => Set<KitchenStation>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<DiscountPolicy> DiscountPolicies => Set<DiscountPolicy>();
    public DbSet<DiscountPolicyCondition> DiscountPolicyConditions => Set<DiscountPolicyCondition>();

    // --- Area E: Sales Transaction ---
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<CancellationReason> CancellationReasons => Set<CancellationReason>();
    public DbSet<Denomination> Denominations => Set<Denomination>();
    public DbSet<ShiftSession> ShiftSessions => Set<ShiftSession>();
    public DbSet<ShiftSessionCashCount> ShiftSessionCashCounts => Set<ShiftSessionCashCount>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CartItemDetail> CartItemDetails => Set<CartItemDetail>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemDetail> OrderItemDetails => Set<OrderItemDetail>();
    public DbSet<TicketPaymentDetail> TicketPaymentDetails => Set<TicketPaymentDetail>();
    public DbSet<TicketItemSum> TicketItemSums => Set<TicketItemSum>();
    public DbSet<EInvoice> EInvoices => Set<EInvoice>();

    // --- Area F: Reservation ---
    public DbSet<Reservation> Reservations => Set<Reservation>();

    // --- Area H: Inventory & Recipe ---
    public DbSet<ItemUomConversion> ItemUomConversions => Set<ItemUomConversion>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<ItemStock> ItemStocks => Set<ItemStock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    // --- Area I: AI Operations Assistant ---
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiToolCallLog> AiToolCallLogs => Set<AiToolCallLog>();
    public DbSet<AiNotification> AiNotifications => Set<AiNotification>();
    public DbSet<RagDocumentChunk> RagDocumentChunks => Set<RagDocumentChunk>();

    // --- Cross-cutting: Audit ---
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // --- Outbox infrastructure ---
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<OutboxMessageConsumer> OutboxMessageConsumers => Set<OutboxMessageConsumer>();
}
