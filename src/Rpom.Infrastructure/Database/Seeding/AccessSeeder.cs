using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Access;
using Rpom.Domain.Access;

namespace Rpom.Infrastructure.Database.Seeding;

/// <summary>
///     Idempotent seeder for Area A (Access). Run once on app startup.
///     Seeds:
///     1. 8 PermissionGroups (UI grouping)
///     2. ~33 Permissions (from <see cref="Permissions" /> catalog)
///     3. 6 system Roles (labels only)
///     4. 1 bootstrap Owner StaffAccount (creds from <see cref="BootstrapOptions" />)
///     5. Grants ALL permissions to the bootstrap Owner
/// </summary>
public sealed class AccessSeeder(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<BootstrapOptions> bootstrapOptions,
    ILogger<AccessSeeder> logger)
{
    private readonly BootstrapOptions _bootstrap = bootstrapOptions.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        IDateTimeProvider clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        await SeedPermissionGroupsAsync(db, cancellationToken);
        await SeedPermissionsAsync(db, cancellationToken);
        await SeedModulesAsync(db, cancellationToken);
        await SeedPagesAsync(db, cancellationToken);
        await SeedRolesAsync(db, clock, cancellationToken);
        await SeedBootstrapOwnerAsync(db, passwordHasher, clock, cancellationToken);

        logger.LogInformation("AccessSeeder finished.");
    }

    // ----- Step 1: PermissionGroups ------------------------------------------

    private static async Task SeedPermissionGroupsAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var groups = new (string Code, string Name, short Order)[]
        {
            (PermissionGroups.Common, "Common", 10),
            (PermissionGroups.MasterData, "Master Data", 20),
            (PermissionGroups.Pos, "POS Operations", 30),
            (PermissionGroups.Kds, "Kitchen Display", 40),
            (PermissionGroups.Cashier, "Cashier", 50),
            (PermissionGroups.Reporting, "Reporting", 60),
            (PermissionGroups.Ai, "AI Assistant", 70),
            (PermissionGroups.Access, "Access Control", 80)
        };

        List<string> existing = await db.PermissionGroups
            .Select(x => x.Code).ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        foreach ((string Code, string Name, short Order) g in groups)
        {
            if (existingSet.Contains(g.Code))
            {
                continue;
            }

            db.PermissionGroups.Add(new PermissionGroup
            {
                Code = g.Code,
                Name = g.Name,
                DisplayOrder = g.Order
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ----- Step 2: Permissions -----------------------------------------------

    private static async Task SeedPermissionsAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        Dictionary<string, int> groupIdByCode = await db.PermissionGroups
            .ToDictionaryAsync(x => x.Code, x => x.Id, ct);

        var catalog = new (string Code, string Name, string Group)[]
        {
            (Permissions.StaffLogin, "Login to the app", PermissionGroups.Common),

            (Permissions.MasterDataView, "View master data", PermissionGroups.MasterData),
            (Permissions.MasterDataManage, "Create/edit/delete master data", PermissionGroups.MasterData),

            (Permissions.TicketOpen, "Open ticket on table", PermissionGroups.Pos),
            (Permissions.TicketViewDetail, "View ticket detail", PermissionGroups.Pos),
            (Permissions.TicketTransfer, "Transfer ticket between tables", PermissionGroups.Pos),
            (Permissions.TicketMerge, "Merge tickets", PermissionGroups.Pos),
            (Permissions.TicketSplit, "Split ticket items to another ticket", PermissionGroups.Pos),
            (Permissions.TicketCancel, "Cancel ticket with reason", PermissionGroups.Pos),
            (Permissions.OrderAddItems, "Add items to cart", PermissionGroups.Pos),
            (Permissions.OrderSendKitchen, "Send order to kitchen", PermissionGroups.Pos),
            (Permissions.OrderItemCancelPending, "Cancel pending dish (out-of-stock)", PermissionGroups.Pos),
            (Permissions.OrderItemRefundLine, "Refund a damaged dish", PermissionGroups.Pos),
            (Permissions.ReservationView, "View reservation list", PermissionGroups.Pos),
            (Permissions.ReservationCreate, "Create reservation", PermissionGroups.Pos),
            (Permissions.ReservationSeat, "Seat a reservation (open tickets)", PermissionGroups.Pos),
            (Permissions.ReservationCancel, "Cancel reservation", PermissionGroups.Pos),
            (Permissions.TicketList, "List all tickets across statuses", PermissionGroups.Pos),
            (Permissions.TicketAuditLog, "View ticket audit/history log", PermissionGroups.Pos),

            (Permissions.KdsView, "View kitchen display", PermissionGroups.Kds),
            (Permissions.OrderItemStartCooking, "Mark dish PROCESSING", PermissionGroups.Kds),
            (Permissions.OrderItemMarkReady, "Mark dish READY", PermissionGroups.Kds),
            (Permissions.OrderItemProcessReturn, "Process a return line + optional restock", PermissionGroups.Kds),
            (Permissions.ItemToggleAvailability, "Toggle item out-of-stock availability", PermissionGroups.Kds),
            (Permissions.NotificationView, "View operational notifications", PermissionGroups.Pos),
            (Permissions.OrderItemMarkDone, "Mark dish DONE (served)", PermissionGroups.Pos),

            (Permissions.CashDrawerOpen, "Open cash drawer at counter", PermissionGroups.Cashier),
            (Permissions.CashDrawerClose, "Close cash drawer (any opener)", PermissionGroups.Cashier),
            (Permissions.PaymentCash, "Process cash payment", PermissionGroups.Cashier),
            (Permissions.PaymentQr, "Process QR payment", PermissionGroups.Cashier),
            (Permissions.PaymentCancelPending, "Cancel pending payment", PermissionGroups.Cashier),
            (Permissions.PaymentDeleteRecord, "Soft-delete payment record", PermissionGroups.Cashier),
            (Permissions.TicketApplyDiscount, "Apply discount policy at payment", PermissionGroups.Cashier),
            (Permissions.TicketClose, "Close ticket after payment", PermissionGroups.Cashier),
            (Permissions.EInvoiceIssue, "Issue VAT e-invoice", PermissionGroups.Cashier),
            (Permissions.CashierFloorPlan, "View cashier floor plan", PermissionGroups.Cashier),
            (Permissions.CashierViewTicket, "View ticket (cashier)", PermissionGroups.Cashier),
            (Permissions.CashierViewMenu, "View cashier menu", PermissionGroups.Cashier),

            (Permissions.ReportRevenue, "View revenue reports", PermissionGroups.Reporting),
            (Permissions.ReportShift, "View shift reports", PermissionGroups.Reporting),
            (Permissions.ReportItemConsumption, "View item-consumption reports", PermissionGroups.Reporting),
            (Permissions.ReportExportExcel, "Export reports to Excel", PermissionGroups.Reporting),

            (Permissions.AiAsk, "Ask AI Operations Assistant", PermissionGroups.Ai),
            (Permissions.AiViewNotifications, "View AI notifications", PermissionGroups.Ai),

            (Permissions.StaffAccountManage, "Manage staff accounts", PermissionGroups.Access),
            (Permissions.RoleManage, "Manage roles", PermissionGroups.Access),
            (Permissions.PermissionAssign, "Assign permissions to accounts", PermissionGroups.Access),
            (Permissions.PageAccessAssign, "Assign page access to accounts", PermissionGroups.Access),

            (Permissions.ConfigView, "View configuration values", PermissionGroups.Access),
            (Permissions.ConfigManage, "Update configuration values", PermissionGroups.Access),
            (Permissions.UpdateRoundingConfig, "Update rounding precision config", PermissionGroups.Access)
        };

        List<string> existingCodes = await db.Permissions.Select(x => x.Code).ToListAsync(ct);
        var existingSet = existingCodes.ToHashSet();

        short order = 0;
        foreach ((string Code, string Name, string Group) p in catalog)
        {
            if (existingSet.Contains(p.Code))
            {
                order++;
                continue;
            }

            db.Permissions.Add(new Permission
            {
                Code = p.Code,
                Name = p.Name,
                PermissionGroupId = groupIdByCode[p.Group],
                DisplayOrder = order++
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ----- Step 2b: Modules ---------------------------------------------------

    private static async Task SeedModulesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var modules = new (string Code, string Name, short Order)[]
        {
            (Modules.NextErp, "NextERP", 10),
            (Modules.Cashier, "Cashier", 20),
            (Modules.Order, "Order", 30),
            (Modules.Kitchen, "Kitchen", 40)
        };

        var existing = (await db.Modules.Select(x => x.Code).ToListAsync(ct)).ToHashSet();

        foreach ((string Code, string Name, short Order) m in modules)
        {
            if (existing.Contains(m.Code))
            {
                continue;
            }

            db.Modules.Add(new Module { Code = m.Code, Name = m.Name, DisplayOrder = m.Order });
        }

        await db.SaveChangesAsync(ct);
    }

    // ----- Step 2c: Pages -----------------------------------------------------

    private static async Task SeedPagesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        Dictionary<string, int> moduleIdByCode =
            await db.Modules.ToDictionaryAsync(x => x.Code, x => x.Id, ct);

        var catalog = new (string Code, string Name, string Module)[]
        {
            // NextERP — Mặt bằng
            (Pages.NextErpCounters, "Quầy", Modules.NextErp),
            (Pages.NextErpAreas, "Khu vực", Modules.NextErp),
            (Pages.NextErpAreaMenuCategory, "Menu theo khu", Modules.NextErp),
            (Pages.NextErpTables, "Bàn / Phòng / Máy", Modules.NextErp),

            // NextERP — Thực đơn
            (Pages.NextErpItems, "Hàng hóa / Dịch vụ", Modules.NextErp),
            (Pages.NextErpUom, "Đơn vị tính", Modules.NextErp),
            (Pages.NextErpUomConversion, "Quy đổi ĐVT", Modules.NextErp),
            (Pages.NextErpChoiceCategories, "Loại lựa chọn", Modules.NextErp),
            (Pages.NextErpSetMenu, "Set Menu", Modules.NextErp),
            (Pages.NextErpKitchenStations, "Bếp con", Modules.NextErp),

            // NextERP — Kho
            (Pages.NextErpStock, "Tồn kho", Modules.NextErp),
            (Pages.NextErpStockMovement, "Nhập/Xuất kho", Modules.NextErp),

            // NextERP — Giá & Khuyến mãi
            (Pages.NextErpPricing, "Bảng giá bán", Modules.NextErp),
            (Pages.NextErpDiscountPolicies, "Chính sách giảm giá", Modules.NextErp),

            // NextERP — Hệ thống
            (Pages.NextErpStaffAccounts, "Quản lý tài khoản & phân quyền", Modules.NextErp),
            (Pages.NextErpShifts, "Danh sách ca", Modules.NextErp),
            (Pages.NextErpCancellationReasons, "Lý do huỷ/trả", Modules.NextErp),

            // NextERP — Future (uncomment khi window được build):
            // (Pages.NextErpFloorPlan, "Sơ đồ bàn", Modules.NextErp),
            // (Pages.NextErpServiceCharge, "Phí phục vụ", Modules.NextErp),
            // (Pages.NextErpSchedule, "Lịch làm việc", Modules.NextErp),
            // (Pages.NextErpConfig, "Cấu hình nhà hàng", Modules.NextErp),
            // (Pages.NextErpReports, "Báo cáo & Phân tích", Modules.NextErp),
            // (Pages.NextErpAi, "AI Conversational", Modules.NextErp),

            (Pages.CashierFloorPlan, "Floor Plan", Modules.Cashier),
            (Pages.CashierTickets, "Tickets", Modules.Cashier),
            (Pages.CashierMenu, "Menu", Modules.Cashier),
            (Pages.CashierPayment, "Payment", Modules.Cashier),
            (Pages.CashierCashDrawer, "Cash Drawer", Modules.Cashier),

            (Pages.OrderFloorPlan, "Floor Plan", Modules.Order),
            (Pages.OrderTickets, "Tickets", Modules.Order),
            (Pages.OrderMenu, "Menu", Modules.Order),

            (Pages.KitchenKds, "Kitchen Display", Modules.Kitchen),
            (Pages.KitchenStations, "Stations", Modules.Kitchen),
            (Pages.KitchenIngredients, "Ingredients", Modules.Kitchen)
        };

        var existing = (await db.Pages.Select(x => x.Code).ToListAsync(ct)).ToHashSet();

        short order = 0;
        foreach ((string Code, string Name, string Module) p in catalog)
        {
            if (existing.Contains(p.Code))
            {
                order++;
                continue;
            }

            db.Pages.Add(new Page
            {
                Code = p.Code,
                Name = p.Name,
                ModuleId = moduleIdByCode[p.Module],
                DisplayOrder = order++
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ----- Step 3: Roles ----------------------------------------------------

    private static async Task SeedRolesAsync(
        ApplicationDbContext db, IDateTimeProvider clock, CancellationToken ct)
    {
        var roles = new (string Code, string Name)[]
        {
            (Roles.Owner, "Owner"),
            (Roles.Manager, "Manager"),
            (Roles.Cashier, "Cashier"),
            (Roles.OrderStaff, "Order Staff"),
            (Roles.KitchenStaff, "Kitchen Staff"),
            (Roles.AdminVendor, "Admin (Vendor)")
        };

        var existing = (await db.Roles.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        DateTime now = clock.UtcNow;

        foreach ((string Code, string Name) r in roles)
        {
            if (existing.Contains(r.Code))
            {
                continue;
            }

            db.Roles.Add(new Role
            {
                Code = r.Code,
                Name = r.Name,
                IsSystemRole = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ----- Step 4: Bootstrap Owner account + grant all permissions -----------

    private async Task SeedBootstrapOwnerAsync(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IDateTimeProvider clock,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_bootstrap.OwnerPassword))
        {
            logger.LogWarning(
                "Bootstrap.OwnerPassword is empty — skipping bootstrap Owner seed. "
                + "Set the Bootstrap section in appsettings.json to enable.");
            return;
        }

        StaffAccount? owner = await db.StaffAccounts
            .FirstOrDefaultAsync(x => x.Username == _bootstrap.OwnerUsername, ct);

        if (owner is not null)
        {
            // Sync: ensure existing bootstrap Owner has every permission newly added
            // to the catalog (e.g. when devs add a new permission code between releases).
            await SyncOwnerPermissionsAsync(db, owner.Id, clock.UtcNow, ct);
            await SyncOwnerPageAccessAsync(db, owner.Id, clock.UtcNow, ct);
            return;
        }

        Role ownerRole = await db.Roles
                             .FirstOrDefaultAsync(x => x.Code == Roles.Owner, ct)
                         ?? throw new InvalidOperationException("OWNER role not seeded — seed order broken.");

        DateTime now = clock.UtcNow;
        owner = new StaffAccount
        {
            Username = _bootstrap.OwnerUsername,
            PasswordHash = passwordHasher.Hash(_bootstrap.OwnerPassword),
            FullName = _bootstrap.OwnerFullName,
            Email = _bootstrap.OwnerEmail,
            Phone = _bootstrap.OwnerPhone,
            RoleId = ownerRole.Id,
            IsActive = true,
            IsLocked = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.StaffAccounts.Add(owner);
        await db.SaveChangesAsync(ct); // need owner.Id

        // Grant every permission to bootstrap Owner.
        List<int> allPermissionIds = await db.Permissions.Select(x => x.Id).ToListAsync(ct);
        foreach (int pid in allPermissionIds)
        {
            db.StaffAccountPermissions.Add(new StaffAccountPermission
            {
                StaffAccountId = owner.Id,
                PermissionId = pid,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        // Grant every page to bootstrap Owner (full navigation access).
        List<int> allPageIds = await db.Pages.Select(x => x.Id).ToListAsync(ct);
        foreach (int pageId in allPageIds)
        {
            db.StaffAccountPageAccesses.Add(new StaffAccountPageAccess
            {
                StaffAccountId = owner.Id,
                PageId = pageId,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Bootstrap Owner created: username='{Username}', granted {Count} permissions. "
            + "→ Login with provided password, then change it ASAP.",
            _bootstrap.OwnerUsername, allPermissionIds.Count);
    }

    /// <summary>
    ///     Idempotent permission sync — grants every Permission row that the
    ///     bootstrap Owner doesn't already have. Used on app restart to pick up
    ///     newly added permissions in the catalog.
    /// </summary>
    private static async Task SyncOwnerPermissionsAsync(
        ApplicationDbContext db, int ownerId, DateTime now, CancellationToken ct)
    {
        List<int> allPermissionIds = await db.Permissions.Select(x => x.Id).ToListAsync(ct);
        var grantedIds = (await db.StaffAccountPermissions
            .Where(x => x.StaffAccountId == ownerId)
            .Select(x => x.PermissionId)
            .ToListAsync(ct)).ToHashSet();

        foreach (int pid in allPermissionIds)
        {
            if (grantedIds.Contains(pid))
            {
                continue;
            }

            db.StaffAccountPermissions.Add(new StaffAccountPermission
            {
                StaffAccountId = ownerId,
                PermissionId = pid,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    ///     Idempotent page-access sync — grants every Page row that the bootstrap
    ///     Owner doesn't already have. Used on app restart to pick up newly added pages.
    /// </summary>
    private static async Task SyncOwnerPageAccessAsync(
        ApplicationDbContext db, int ownerId, DateTime now, CancellationToken ct)
    {
        List<int> allPageIds = await db.Pages.Select(x => x.Id).ToListAsync(ct);
        var grantedIds = (await db.StaffAccountPageAccesses
            .Where(x => x.StaffAccountId == ownerId)
            .Select(x => x.PageId)
            .ToListAsync(ct)).ToHashSet();

        foreach (int pageId in allPageIds)
        {
            if (grantedIds.Contains(pageId))
            {
                continue;
            }

            db.StaffAccountPageAccesses.Add(new StaffAccountPageAccess
            {
                StaffAccountId = ownerId,
                PageId = pageId,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
