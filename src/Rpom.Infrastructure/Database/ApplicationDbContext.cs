using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Domain.Access;
using Rpom.Domain.Ai;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Configuration;
using Rpom.Domain.Inventory;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Infrastructure.Database;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IDbContext
{
    // --- Area A: Access (RBAC) ---
    public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();
    public DbSet<StaffAccountPermission> StaffAccountPermissions => Set<StaffAccountPermission>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<StaffAccountPageAccess> StaffAccountPageAccesses => Set<StaffAccountPageAccess>();

    // --- Area B: Restaurant ---
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<TableLock> TableLocks => Set<TableLock>();

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
    public DbSet<StaffNotification> StaffNotifications => Set<StaffNotification>();
    public DbSet<NotificationReadState> NotificationReadStates => Set<NotificationReadState>();
    public DbSet<ItemAreaLock> ItemAreaLocks => Set<ItemAreaLock>();
    public DbSet<PosTerminal> PosTerminals => Set<PosTerminal>();
    public DbSet<CustomerDisplay> CustomerDisplays => Set<CustomerDisplay>();
    public DbSet<DiscountPolicy> DiscountPolicies => Set<DiscountPolicy>();
    public DbSet<DiscountPolicyCondition> DiscountPolicyConditions => Set<DiscountPolicyCondition>();

    // --- Area E: Sales Transaction ---
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<CancellationReason> CancellationReasons => Set<CancellationReason>();
    public DbSet<Denomination> Denominations => Set<Denomination>();
    public DbSet<CashDrawerSession> CashDrawerSessions => Set<CashDrawerSession>();
    public DbSet<CashDrawerCashCount> CashDrawerCashCounts => Set<CashDrawerCashCount>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CartItemDetail> CartItemDetails => Set<CartItemDetail>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemDetail> OrderItemDetails => Set<OrderItemDetail>();
    public DbSet<TicketPaymentDetail> TicketPaymentDetails => Set<TicketPaymentDetail>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<TicketItemSum> TicketItemSums => Set<TicketItemSum>();
    public DbSet<TicketInvoice> TicketInvoices => Set<TicketInvoice>();
    public DbSet<TicketInvoiceLine> TicketInvoiceLines => Set<TicketInvoiceLine>();
    public DbSet<EInvoice> EInvoices => Set<EInvoice>();

    // --- Area F: Reservation ---
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationTable> ReservationTables => Set<ReservationTable>();

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

    // --- Cross-cutting: Aggregate polling versions ---
    public DbSet<DomainVersion> DomainVersions => Set<DomainVersion>();

    // --- Cross-cutting: Configuration ---
    public DbSet<ConfigValue> ConfigValues => Set<ConfigValue>();
    public DbSet<RoundingConfig> RoundingConfigs => Set<RoundingConfig>();

    // --- Outbox infrastructure ---
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<OutboxMessageConsumer> OutboxMessageConsumers => Set<OutboxMessageConsumer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.HasDefaultSchema(Schemas.Default);
    }
}
