using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

namespace Rpom.Application.Abstraction.Data;

/// <summary>
///     EF Core DbContext abstraction exposed to the Application layer.
///     One DbSet per Domain entity (51 entities across 8 areas + cross-cutting).
/// </summary>
public interface IDbContext
{
    // --- Area A: Access (RBAC) ---
    DbSet<PermissionGroup> PermissionGroups { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<Role> Roles { get; }
    DbSet<StaffAccount> StaffAccounts { get; }
    DbSet<StaffAccountPermission> StaffAccountPermissions { get; }
    DbSet<Module> Modules { get; }
    DbSet<Page> Pages { get; }
    DbSet<StaffAccountPageAccess> StaffAccountPageAccesses { get; }

    // --- Area B: Restaurant ---
    DbSet<Counter> Counters { get; }
    DbSet<Area> Areas { get; }
    DbSet<Table> Tables { get; }
    DbSet<TableLock> TableLocks { get; }

    // --- Area C: Menu & Catalog ---
    DbSet<Uom> Uoms { get; }
    DbSet<Category> Categories { get; }
    DbSet<Item> Items { get; }
    DbSet<ChoiceCategory> ChoiceCategories { get; }
    DbSet<PriceTable> PriceTables { get; }
    DbSet<PriceVariant> PriceVariants { get; }
    DbSet<PriceEntry> PriceEntries { get; }
    DbSet<SetMenu> SetMenus { get; }
    DbSet<ItemCategory> ItemCategories { get; }
    DbSet<SetMenuDetail> SetMenuDetails { get; }
    DbSet<Modifier> Modifiers { get; }
    DbSet<AreaMenuCategory> AreaMenuCategories { get; }
    DbSet<PriceVariantArea> PriceVariantAreas { get; }

    // --- Area D: Operations Config ---
    DbSet<Shift> Shifts { get; }
    DbSet<KitchenStation> KitchenStations { get; }
    DbSet<Printer> Printers { get; }
    DbSet<StaffNotification> StaffNotifications { get; }
    DbSet<NotificationReadState> NotificationReadStates { get; }
    DbSet<ItemAreaLock> ItemAreaLocks { get; }
    DbSet<DiscountPolicy> DiscountPolicies { get; }
    DbSet<DiscountPolicyCondition> DiscountPolicyConditions { get; }

    // --- Area E: Sales Transaction ---
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<CancellationReason> CancellationReasons { get; }
    DbSet<Denomination> Denominations { get; }
    DbSet<CashDrawerSession> CashDrawerSessions { get; }
    DbSet<CashDrawerCashCount> CashDrawerCashCounts { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<Order> Orders { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<CartItemDetail> CartItemDetails { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderItemDetail> OrderItemDetails { get; }
    DbSet<TicketPaymentDetail> TicketPaymentDetails { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<TicketItemSum> TicketItemSums { get; }
    DbSet<EInvoice> EInvoices { get; }

    // --- Area F: Reservation ---
    DbSet<Reservation> Reservations { get; }

    // --- Area H: Inventory & Recipe ---
    DbSet<ItemUomConversion> ItemUomConversions { get; }
    DbSet<BomLine> BomLines { get; }
    DbSet<ItemStock> ItemStocks { get; }
    DbSet<StockMovement> StockMovements { get; }

    // --- Area I: AI Operations Assistant ---
    DbSet<AiConversation> AiConversations { get; }
    DbSet<AiMessage> AiMessages { get; }
    DbSet<AiToolCallLog> AiToolCallLogs { get; }
    DbSet<AiNotification> AiNotifications { get; }
    DbSet<RagDocumentChunk> RagDocumentChunks { get; }

    // --- Cross-cutting: Audit ---
    DbSet<AuditLog> AuditLogs { get; }

    // --- Cross-cutting: Aggregate polling versions ---
    DbSet<DomainVersion> DomainVersions { get; }

    // --- Cross-cutting: Configuration ---
    DbSet<ConfigValue> ConfigValues { get; }
    DbSet<RoundingConfig> RoundingConfigs { get; }

    // --- Outbox infrastructure ---
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<OutboxMessageConsumer> OutboxMessageConsumers { get; }

    DatabaseFacade Database { get; }

    /// <summary>
    ///     Change tracker — chỉ dùng cho thao tác đặc thù như dry-run rollback (preview) cần
    ///     reset tracked state giữa các lần retry của execution strategy.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
