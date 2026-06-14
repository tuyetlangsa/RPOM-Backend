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
        await SeedSetMenuDataAsync(db, now, ct);

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

        // Generate a realistic staffing roster (password "123" for every demo account).
        var accounts = new List<(string Username, string Password, string FullName, string Role)>();
        void AddBatch(string prefix, int count, string fullNamePrefix, string role)
        {
            for (int i = 1; i <= count; i++)
            {
                accounts.Add(($"{prefix}{i:D2}", "123", $"{fullNamePrefix} {i:D2}", role));
            }
        }

        AddBatch("cashier", 10, "Thu ngân", Roles.Cashier);
        AddBatch("order", 10, "Phục vụ", Roles.OrderStaff);
        AddBatch("kitchen", 10, "Đầu bếp", Roles.KitchenStaff);
        AddBatch("manager", 3, "Quản lý", Roles.Manager);
        accounts.Add(("owner01", "123", "Chủ nhà hàng", Roles.Owner));

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
                Permissions.TicketTransfer,
                Permissions.TicketCancel,
                Permissions.OrderAddItems,
                Permissions.OrderSendKitchen,
                Permissions.OrderItemCancelPending,
                Permissions.OrderItemMarkDone,
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
                Permissions.TicketTransfer,
                Permissions.OrderAddItems,
                Permissions.OrderSendKitchen,
            },
            [Roles.KitchenStaff] = new[]
            {
                Permissions.StaffLogin,
                Permissions.KdsView,
                Permissions.OrderItemStartCooking,
                Permissions.OrderItemMarkReady,
                Permissions.OrderItemMarkDone,
            },
            [Roles.Manager] = new[]
            {
                Permissions.StaffLogin,
                Permissions.MasterDataView,
                Permissions.CashierFloorPlan,
                Permissions.CashierViewTicket,
                Permissions.CashierViewMenu,
                Permissions.TicketOpen,
                Permissions.TicketViewDetail,
                Permissions.TicketTransfer,
                Permissions.TicketMerge,
                Permissions.TicketCancel,
                Permissions.OrderAddItems,
                Permissions.OrderSendKitchen,
                Permissions.OrderItemCancelPending,
                Permissions.OrderItemRefundLine,
                Permissions.OrderItemMarkDone,
                Permissions.TicketApplyDiscount,
                Permissions.TicketClose,
                Permissions.CashDrawerOpen,
                Permissions.CashDrawerClose,
                Permissions.PaymentCash,
                Permissions.PaymentQr,
                Permissions.PaymentCancelPending,
                Permissions.EInvoiceIssue,
                Permissions.ReservationCreate,
                Permissions.ReservationCancel,
                Permissions.ReportRevenue,
                Permissions.ReportShift,
                Permissions.ReportItemConsumption,
                Permissions.ReportExportExcel,
                Permissions.ConfigView,
            },
            // Owner gets the full permission catalog.
            [Roles.Owner] = allPermissions.Select(p => p.Code).ToArray(),
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

    /// <summary>Base pre-VAT price (VND) per leaf category — varied per item below.</summary>
    private static readonly Dictionary<string, decimal> BasePriceByCategory = new()
    {
        ["KV_GOI"] = 55_000m, ["KV_CHIEN"] = 45_000m, ["KV_CUON"] = 50_000m,
        ["MC_COM"] = 55_000m, ["MC_PHO"] = 55_000m, ["MC_MI"] = 50_000m,
        ["LN_LAU"] = 269_000m, ["LN_NUONG"] = 95_000m,
        ["HS_TOM"] = 159_000m, ["HS_CUA"] = 299_000m, ["HS_CA"] = 129_000m,
        ["MCH_CHAY"] = 55_000m,
        ["TM_CHE"] = 29_000m, ["TM_BANH"] = 35_000m,
        ["DOUONG_BIA"] = 22_000m, ["DOUONG_NGOT"] = 15_000m,
        ["DU_CAPHE"] = 35_000m, ["DU_TRA"] = 45_000m,
        ["CB_SET"] = 120_000m,
    };

    private static decimal RoundThousand(decimal v) =>
        Math.Round(v / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m;

    private static async Task SeedPricingAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.PriceTables.AnyAsync(ct)) return;

        var vipArea = await db.Areas.FirstOrDefaultAsync(a => a.Name == "Khu VIP", ct);

        // 1 PriceTable with 3 variants: default, happy-hour (14-17h), VIP-area (+20%).
        var table = new PriceTable
        {
            Code = "PT-DEFAULT",
            Name = "Bảng giá nhà hàng",
            Description = "Seed tự động — giá gốc + happy hour + khu VIP",
            BeginDate = new DateOnly(2024, 1, 1),
            EndDate = null,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PriceTables.Add(table);
        await db.SaveChangesAsync(ct);

        var vDefault = new PriceVariant
        {
            PriceTableId = table.Id,
            Code = "PV-DEFAULT",
            Name = "Giá cơ bản",
            AppliesToAllAreas = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var vHappy = new PriceVariant
        {
            PriceTableId = table.Id,
            Code = "PV-HAPPY",
            Name = "Happy Hour 14h-17h",
            BeginTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(17, 0),
            AppliesToAllAreas = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var vVip = new PriceVariant
        {
            PriceTableId = table.Id,
            Code = "PV-VIP",
            Name = "Giá khu VIP",
            AppliesToAllAreas = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PriceVariants.AddRange(vDefault, vHappy, vVip);
        await db.SaveChangesAsync(ct);

        if (vipArea is not null)
        {
            db.PriceVariantAreas.Add(new PriceVariantArea
            {
                PriceVariantId = vVip.Id,
                AreaId = vipArea.Id,
                CreatedAt = now,
            });
        }

        // All sellable items under HANG_BAN (combos are priced later in the set-menu seed).
        Category? hangBan = await db.Categories.FirstOrDefaultAsync(c => c.Code == "HANG_BAN", ct);
        if (hangBan is null) return;

        var hangBanCatIds = await db.Categories
            .Where(c => c.Path.Contains($"{hangBan.Id};") || c.Id == hangBan.Id)
            .Select(c => c.Id)
            .ToListAsync(ct);

        // Map each sellable item to its MAIN category code to look up a base price.
        var rows = await db.ItemCategories
            .Where(ic => ic.IsMain && hangBanCatIds.Contains(ic.CategoryId))
            .Select(ic => new { ic.ItemId, ic.Category.Code })
            .ToListAsync(ct);

        foreach (var r in rows)
        {
            if (!BasePriceByCategory.TryGetValue(r.Code, out decimal baseP))
            {
                continue;
            }

            // Deterministic per-item variation so the menu isn't flat.
            decimal price = baseP + (r.ItemId % 5) * 5_000m;
            // Drinks (cà phê, trà) are commonly quoted VAT-included; demo both code paths.
            bool vatIncluded = r.Code is "DU_CAPHE" or "DU_TRA";

            db.PriceEntries.Add(new PriceEntry
            {
                PriceVariantId = vDefault.Id, ItemId = r.ItemId,
                Price = price, IsVatIncluded = vatIncluded, CreatedAt = now, UpdatedAt = now,
            });
            db.PriceEntries.Add(new PriceEntry
            {
                PriceVariantId = vHappy.Id, ItemId = r.ItemId,
                Price = RoundThousand(price * 0.85m), IsVatIncluded = vatIncluded, CreatedAt = now, UpdatedAt = now,
            });
            db.PriceEntries.Add(new PriceEntry
            {
                PriceVariantId = vVip.Id, ItemId = r.ItemId,
                Price = RoundThousand(price * 1.20m), IsVatIncluded = vatIncluded, CreatedAt = now, UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ─── Area ↔ Menu Categories ──────────────────────────────────────────────

    private static async Task SeedAreaMenuCategoriesAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.AreaMenuCategories.AnyAsync(ct)) return;

        var areas = await db.Areas.Where(a => a.IsActive).ToListAsync(ct);
        // Assign the top-level Hàng bán groups to every area; descendants are included via Path.
        var groupCodes = new[]
        {
            "KHAIVI", "MONCHINH", "LAUNUONG", "HAISAN", "MONCHAY", "TRANGMIENG", "DOUONG", "COMBO"
        };
        var categories = await db.Categories.Where(c => groupCodes.Contains(c.Code)).ToListAsync(ct);

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
        Item? biaItem = await db.Items
            .Where(i => i.ItemCategories.Any(ic => ic.Category.Code == "DOUONG_BIA"))
            .OrderBy(i => i.Id)
            .FirstOrDefaultAsync(ct);
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

        // Policy 3: TICKET_THRESHOLD — bill ≥ 1,000k → giảm 100k cố định (FIXED)
        var p3 = new DiscountPolicy
        {
            Code = "GIAM100",
            Name = "Bill trên 1 triệu giảm 100k",
            Description = "Tự động áp dụng khi bill ≥ 1.000.000đ — giảm thẳng 100.000đ",
            DiscountType = DiscountType.TicketThreshold,
            IsAutoApply = true,
            DaysOfWeek = null,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        p3.Conditions.Add(new DiscountPolicyCondition
        {
            DiscountPolicy = p3,
            ThresholdAmount = 1_000_000m,
            AreaId = null,
            ApplyType = DiscountApplyType.Fixed,
            DiscountValue = 100_000m,
            DisplayOrder = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });

        // Policy 4: QUANTITY_ITEM — 3 Phở → giảm 30k (FIXED per-item)
        Item? phoItem = await db.Items
            .Where(i => i.ItemCategories.Any(ic => ic.Category.Code == "MC_PHO"))
            .OrderBy(i => i.Id)
            .FirstOrDefaultAsync(ct);
        if (phoItem is not null)
        {
            var p4 = new DiscountPolicy
            {
                Code = "GIAM_PHO",
                Name = "Mua 3 Phở giảm 30k",
                Description = "Khách mua ≥ 3 Phở bò tái → giảm 30.000đ tiền phở",
                DiscountType = DiscountType.QuantityItem,
                IsAutoApply = false,
                DaysOfWeek = null,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            p4.Conditions.Add(new DiscountPolicyCondition
            {
                DiscountPolicy = p4,
                ItemId = phoItem.Id,
                QuantityThreshold = 3m,
                AreaId = null,
                ApplyType = DiscountApplyType.Fixed,
                DiscountValue = 30_000m,
                DisplayOrder = 1,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.DiscountPolicies.Add(p4);
        }

        // Policy 5: TICKET_THRESHOLD — bill ≥ 500k, chỉ áp dụng T2-T6 (manual, PERCENT)
        var p5 = new DiscountPolicy
        {
            Code = "GIAM_TUAN",
            Name = "Bill 500k giảm 5% (T2-T6)",
            Description = "Áp dụng tay, bill ≥ 500k vào thứ 2-6 → giảm 5%",
            DiscountType = DiscountType.TicketThreshold,
            IsAutoApply = false,
            DaysOfWeek = "1,2,3,4,5", // Mon-Fri
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        p5.Conditions.Add(new DiscountPolicyCondition
        {
            DiscountPolicy = p5,
            ThresholdAmount = 500_000m,
            AreaId = null,
            ApplyType = DiscountApplyType.Percent,
            DiscountValue = 5m,
            DisplayOrder = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });

        db.DiscountPolicies.AddRange(p3, p5);
        await db.SaveChangesAsync(ct);
    }

    // ─── Set Menu Data (combos) ──────────────────────────────────────────────

    private static async Task SeedSetMenuDataAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.SetMenuDetails.AnyAsync(ct)) return;

        PriceVariant vDefault = await db.PriceVariants.FirstAsync(v => v.Code == "PV-DEFAULT", ct);
        PriceVariant vHappy = await db.PriceVariants.FirstAsync(v => v.Code == "PV-HAPPY", ct);
        PriceVariant vVip = await db.PriceVariants.FirstAsync(v => v.Code == "PV-VIP", ct);
        Uom suat = await db.Uoms.FirstAsync(u => u.Code == "suat", ct);
        Category cbSet = await db.Categories.FirstAsync(c => c.Code == "CB_SET", ct);

        async Task<List<Item>> ItemsIn(string code) => await db.Items
            .Where(i => i.ItemCategories.Any(ic => ic.Category.Code == code))
            .OrderBy(i => i.Id)
            .ToListAsync(ct);

        var mains = (await ItemsIn("MC_COM"))
            .Concat(await ItemsIn("MC_PHO"))
            .Concat(await ItemsIn("MC_MI"))
            .Concat(await ItemsIn("LN_NUONG"))
            .ToList();
        List<Item> drinks = await ItemsIn("DOUONG_NGOT");
        List<Item> apps = await ItemsIn("KV_CHIEN");
        List<Item> desserts = await ItemsIn("TM_CHE");

        if (mains.Count == 0 || drinks.Count == 0)
        {
            return;
        }

        // ── Shared choice categories + their modifiers ──
        var ccDoiNuoc = new ChoiceCategory
        {
            Name = "Đổi nước", MinChoice = 0, MaxChoice = 1, DisplayOrder = 1,
            IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var ccThemKhaiVi = new ChoiceCategory
        {
            Name = "Thêm khai vị", MinChoice = 0, MaxChoice = 2, DisplayOrder = 2,
            IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var ccThemTrangMieng = new ChoiceCategory
        {
            Name = "Thêm tráng miệng", MinChoice = 0, MaxChoice = 1, DisplayOrder = 3,
            IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        db.ChoiceCategories.AddRange(ccDoiNuoc, ccThemKhaiVi, ccThemTrangMieng);
        await db.SaveChangesAsync(ct);

        short mOrder = 1;
        foreach (Item d in drinks.Take(5))
        {
            db.Modifiers.Add(new Modifier
            {
                ChoiceCategoryId = ccDoiNuoc.Id, ItemId = d.Id, ExtraPrice = 0m,
                MinPerModifier = 0, MaxPerModifier = 1, DisplayOrder = mOrder++,
                IsActive = true, CreatedAt = now, UpdatedAt = now,
            });
        }

        mOrder = 1;
        foreach (Item a in apps.Take(4))
        {
            db.Modifiers.Add(new Modifier
            {
                ChoiceCategoryId = ccThemKhaiVi.Id, ItemId = a.Id, ExtraPrice = 20_000m,
                MinPerModifier = 0, MaxPerModifier = 1, DisplayOrder = mOrder++,
                IsActive = true, CreatedAt = now, UpdatedAt = now,
            });
        }

        mOrder = 1;
        foreach (Item d in desserts.Take(3))
        {
            db.Modifiers.Add(new Modifier
            {
                ChoiceCategoryId = ccThemTrangMieng.Id, ItemId = d.Id, ExtraPrice = 15_000m,
                MinPerModifier = 0, MaxPerModifier = 1, DisplayOrder = mOrder++,
                IsActive = true, CreatedAt = now, UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);

        // ── 30 combos ──
        for (int i = 0; i < 30; i++)
        {
            Item main = mains[i % mains.Count];
            Item drink = drinks[i % drinks.Count];

            var combo = new Item
            {
                Code = $"COMBO_{i + 1:D3}",
                Name = $"Combo {main.Name}",
                BaseUomId = suat.Id,
                VatPercent = 8m,
                IsStockable = false,
                HasRecipe = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Items.Add(combo);
            await db.SaveChangesAsync(ct);

            db.ItemCategories.Add(new ItemCategory
            {
                ItemId = combo.Id, CategoryId = cbSet.Id, IsMain = true, CreatedAt = now,
            });

            decimal price = 99_000m + (i % 6) * 10_000m;
            db.PriceEntries.Add(new PriceEntry
            {
                PriceVariantId = vDefault.Id, ItemId = combo.Id, Price = price,
                IsVatIncluded = false, CreatedAt = now, UpdatedAt = now,
            });
            db.PriceEntries.Add(new PriceEntry
            {
                PriceVariantId = vHappy.Id, ItemId = combo.Id, Price = RoundThousand(price * 0.85m),
                IsVatIncluded = false, CreatedAt = now, UpdatedAt = now,
            });
            db.PriceEntries.Add(new PriceEntry
            {
                PriceVariantId = vVip.Id, ItemId = combo.Id, Price = RoundThousand(price * 1.20m),
                IsVatIncluded = false, CreatedAt = now, UpdatedAt = now,
            });

            db.SetMenus.Add(new SetMenu
            {
                ItemId = combo.Id,
                Description = $"Combo gồm {main.Name} + nước uống",
                CreatedAt = now,
                UpdatedAt = now,
            });

            db.SetMenuDetails.Add(new SetMenuDetail
            {
                SetMenuItemId = combo.Id, DetailType = SetMenuDetailType.Component,
                ComponentItemId = main.Id, Quantity = 1m, IsFixed = true, DisplayOrder = 1,
                CreatedAt = now, UpdatedAt = now,
            });
            db.SetMenuDetails.Add(new SetMenuDetail
            {
                SetMenuItemId = combo.Id, DetailType = SetMenuDetailType.Component,
                ComponentItemId = drink.Id, Quantity = 1m, IsFixed = true, DisplayOrder = 2,
                CreatedAt = now, UpdatedAt = now,
            });
            db.SetMenuDetails.Add(new SetMenuDetail
            {
                SetMenuItemId = combo.Id, DetailType = SetMenuDetailType.ChoiceCategory,
                ChoiceCategoryId = ccDoiNuoc.Id, DisplayOrder = 3, CreatedAt = now, UpdatedAt = now,
            });
            db.SetMenuDetails.Add(new SetMenuDetail
            {
                SetMenuItemId = combo.Id, DetailType = SetMenuDetailType.ChoiceCategory,
                ChoiceCategoryId = (i % 2 == 0 ? ccThemKhaiVi.Id : ccThemTrangMieng.Id),
                DisplayOrder = 4, CreatedAt = now, UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
