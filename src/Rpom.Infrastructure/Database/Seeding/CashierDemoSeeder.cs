using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Access;
using Rpom.Domain.Access;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Seeding;

/// <summary>
///     Idempotent seeder for cashier end-to-end demo data. Creates:
///     - Cashier + Order Staff + Kitchen Staff accounts with appropriate permissions
///     - PriceTable with a single PriceVariant covering all items
///     - AreaMenuCategories so GetMenu returns items per area
///     - DiscountPolicies (one TICKET_THRESHOLD, one QUANTITY_ITEM)
///     - PaymentMethods (Cash + QR)
/// </summary>
public sealed class CashierDemoSeeder(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<CashierDemoSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        IDateTimeProvider clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        DateTime now = clock.UtcNow;

        await SeedStaffAccountsAsync(db, hasher, now, ct);
        await SeedPaymentMethodsAsync(db, now, ct);
        await SeedPricingAsync(db, now, ct);
        await SeedAreaMenuCategoriesAsync(db, now, ct);
        await SeedDiscountPoliciesAsync(db, now, ct);

        logger.LogInformation("CashierDemoSeeder finished.");
    }

    // ─── Staff Accounts ───────────────────────────────────────────────────────

    private static async Task SeedStaffAccountsAsync(
        ApplicationDbContext db, IPasswordHasher hasher, DateTime now, CancellationToken ct)
    {
        var existingUsernames = (await db.StaffAccounts.Select(x => x.Username).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, int> roleIds = await db.Roles
            .ToDictionaryAsync(r => r.Code, r => r.Id, ct);

        var accounts = new (string Username, string Password, string FullName, string Role)[]
        {
            ("thungan", "123", "Nguyễn Thu Ngân", Roles.Cashier),
            ("phucvu", "123", "Trần Phục Vụ", Roles.OrderStaff),
            ("dau-bep", "123", "Lê Đầu Bếp", Roles.KitchenStaff),
        };

        foreach ((string username, string password, string fullName, string role) in accounts)
        {
            if (existingUsernames.Contains(username)) continue;
            if (!roleIds.TryGetValue(role, out int roleId)) continue;

            var staff = new StaffAccount
            {
                Username = username,
                PasswordHash = hasher.Hash(password),
                FullName = fullName,
                RoleId = roleId,
                IsActive = true,
                IsLocked = false,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.StaffAccounts.Add(staff);
        }

        await db.SaveChangesAsync(ct);

        // Grant relevant permissions to each staff account.
        var staffAccounts = await db.StaffAccounts
            .Where(x => existingUsernames.Contains(x.Username) == false)
            .Include(x => x.Role)
            .ToListAsync(ct);

        var allPermissions = await db.Permissions.ToListAsync(ct);
        var permByCode = allPermissions.ToDictionary(p => p.Code);

        var rolePermissions = new Dictionary<string, string[]>
        {
            [Roles.Cashier] = new[]
            {
                Permissions.StaffLogin,
                Permissions.CashierFloorPlan,
                Permissions.CashierViewTicket,
                Permissions.CashierViewMenu,
                Permissions.TicketOpen,
                Permissions.OrderAddItems,
                Permissions.OrderSendKitchen,
                Permissions.TicketApplyDiscount,
                Permissions.CashDrawerOpen,
                Permissions.CashDrawerClose,
                Permissions.PaymentCash,
                Permissions.PaymentQr,
                Permissions.TicketClose,
            },
            [Roles.OrderStaff] = new[]
            {
                Permissions.StaffLogin,
                Permissions.CashierFloorPlan,
                Permissions.CashierViewTicket,
                Permissions.CashierViewMenu,
                Permissions.TicketOpen,
                Permissions.OrderAddItems,
                Permissions.OrderSendKitchen,
            },
            [Roles.KitchenStaff] = new[]
            {
                Permissions.StaffLogin,
                Permissions.KdsView,
                Permissions.OrderItemStartCooking,
                Permissions.OrderItemMarkReady,
            },
        };

        foreach (StaffAccount staff in staffAccounts)
        {
            if (!rolePermissions.TryGetValue(staff.Role.Code, out string[]? permCodes)) continue;

            foreach (string code in permCodes)
            {
                if (!permByCode.TryGetValue(code, out Permission? perm)) continue;

                bool alreadyGranted = await db.StaffAccountPermissions
                    .AnyAsync(x => x.StaffAccountId == staff.Id && x.PermissionId == perm.Id, ct);
                if (alreadyGranted) continue;

                db.StaffAccountPermissions.Add(new StaffAccountPermission
                {
                    StaffAccountId = staff.Id,
                    PermissionId = perm.Id,
                    CreatedAt = now,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ─── Payment Methods ──────────────────────────────────────────────────────

    private static async Task SeedPaymentMethodsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.PaymentMethods.AnyAsync(ct)) return;

        db.PaymentMethods.AddRange(
            new PaymentMethod { Code = "CASH", Name = "Tiền mặt", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new PaymentMethod { Code = "QR", Name = "QR Code", DisplayOrder = 2, IsActive = true, CreatedAt = now, UpdatedAt = now }
        );
        await db.SaveChangesAsync(ct);
    }

    // ─── Pricing ──────────────────────────────────────────────────────────────

    private static async Task SeedPricingAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.PriceTables.AnyAsync(ct)) return;

        // 1 PriceTable, 1 Variant covering all areas, entries for all Hàng bán items.
        var table = new PriceTable
        {
            Code = "PT-DEFAULT",
            Name = "Bảng giá mặc định",
            Description = "Seed tự động — giá gốc cho tất cả món",
            BeginDate = new DateOnly(2024, 1, 1),
            EndDate = null,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PriceTables.Add(table);
        await db.SaveChangesAsync(ct);

        var variant = new PriceVariant
        {
            PriceTableId = table.Id,
            Code = "PV-BASE",
            Name = "Giá cơ bản",
            AppliesToAllAreas = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PriceVariants.Add(variant);
        await db.SaveChangesAsync(ct);

        // Get all Hàng bán items (those under HANG_BAN category tree)
        Category? hangBan = await db.Categories.FirstOrDefaultAsync(c => c.Code == "HANG_BAN", ct);
        if (hangBan is null) return;

        var hangBanIds = await db.Categories
            .Where(c => c.Path.Contains($"{hangBan.Id};") || c.Id == hangBan.Id)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var itemIds = await db.ItemCategories
            .Where(ic => hangBanIds.Contains(ic.CategoryId))
            .Select(ic => ic.ItemId)
            .Distinct()
            .ToListAsync(ct);

        var items = await db.Items
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Name })
            .ToListAsync(ct);

        // Simple price mapping by item name pattern
        foreach (var item in items)
        {
            decimal price = item.Name switch
            {
                string n when n.Contains("Heineken") => 25_000m,
                string n when n.Contains("Tiger") => 22_000m,
                string n when n.Contains("Coca") => 15_000m,
                string n when n.Contains("Pepsi") => 15_000m,
                string n when n.Contains("Lavie") => 10_000m,
                string n when n.Contains("gà xối mỡ") => 55_000m,
                string n when n.Contains("sườn") => 50_000m,
                string n when n.Contains("Phở") => 50_000m,
                string n when n.Contains("Bún bò") => 55_000m,
                string n when n.Contains("Lẩu") => 200_000m,
                string n when n.Contains("Gỏi cuốn") => 45_000m,
                string n when n.Contains("Nem nướng") => 55_000m,
                string n when n.Contains("Chè") => 25_000m,
                string n when n.Contains("flan") => 20_000m,
                _ => 30_000m,
            };

            db.PriceEntries.Add(new PriceEntry
            {
                PriceVariantId = variant.Id,
                ItemId = item.Id,
                Price = price,
                IsVatIncluded = false,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ─── Area ↔ Menu Categories ──────────────────────────────────────────────

    private static async Task SeedAreaMenuCategoriesAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.AreaMenuCategories.AnyAsync(ct)) return;

        var areas = await db.Areas.Where(a => a.IsActive).ToListAsync(ct);
        // Assign all Hàng bán subcategories to all areas
        var subCategoryCodes = new[] { "DOUONG_BIA", "DOUONG_NGOT", "DOUONG_NUOC", "MC_COM", "MC_PHO", "MC_LAU", "MP_KHAIVI", "MP_TRANGMIENG" };
        var categories = await db.Categories.Where(c => subCategoryCodes.Contains(c.Code)).ToListAsync(ct);

        foreach (Area area in areas)
        {
            foreach (Category cat in categories)
            {
                db.AreaMenuCategories.Add(new AreaMenuCategory
                {
                    AreaId = area.Id,
                    CategoryId = cat.Id,
                    CreatedAt = now,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ─── Discount Policies ────────────────────────────────────────────────────

    private static async Task SeedDiscountPoliciesAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.DiscountPolicies.AnyAsync(ct)) return;

        // Policy 1: TICKET_THRESHOLD — bill ≥ 200k → 10%
        var p1 = new DiscountPolicy
        {
            Code = "GIAM10",
            Name = "Bill trên 200k giảm 10%",
            Description = "Tự động áp dụng khi bill ≥ 200.000đ",
            DiscountType = DiscountType.TicketThreshold,
            IsAutoApply = true,
            DaysOfWeek = null,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        p1.Conditions.Add(new DiscountPolicyCondition
        {
            DiscountPolicy = p1,
            ThresholdAmount = 200_000m,
            AreaId = null,
            ApplyType = DiscountApplyType.Percent,
            DiscountValue = 10m,
            DisplayOrder = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        p1.Conditions.Add(new DiscountPolicyCondition
        {
            DiscountPolicy = p1,
            ThresholdAmount = 500_000m,
            AreaId = null,
            ApplyType = DiscountApplyType.Percent,
            DiscountValue = 15m,
            DisplayOrder = 2,
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.DiscountPolicies.Add(p1);

        // Policy 2: QUANTITY_ITEM — 5 bia → giảm 20%
        Item? biaItem = await db.Items.FirstOrDefaultAsync(i => i.Code == "BIA_HEINEKEN", ct);
        if (biaItem is not null)
        {
            var p2 = new DiscountPolicy
            {
                Code = "GIAM_BIA",
                Name = "Mua 5 bia giảm 20%",
                Description = "Khách mua ≥ 5 Heineken → giảm 20% tiền bia",
                DiscountType = DiscountType.QuantityItem,
                IsAutoApply = false,
                DaysOfWeek = null,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            p2.Conditions.Add(new DiscountPolicyCondition
            {
                DiscountPolicy = p2,
                ItemId = biaItem.Id,
                QuantityThreshold = 5m,
                AreaId = null,
                ApplyType = DiscountApplyType.Percent,
                DiscountValue = 20m,
                DisplayOrder = 1,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.DiscountPolicies.Add(p2);
        }

        await db.SaveChangesAsync(ct);
    }
}
