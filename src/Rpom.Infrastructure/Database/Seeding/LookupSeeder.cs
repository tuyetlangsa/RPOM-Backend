using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rpom.Application.Abstraction.Clock;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Seeding;

/// <summary>
///     Idempotent seeder for minimal lookup + master data needed to smoke-test
///     ShiftSession and the Restaurant master-data screens (Counter / Area / Table):
///     - 3 Shifts (MORN, AFTER, NIGHT)
///     - 1 Counter (Quầy Trung Tâm), 2 sample Areas, 5 sample Tables
///     - 2 KitchenStations (Bếp Nóng, Bếp Lạnh)
///     - 7 VND Denominations
///     Owner manages these via Master Data screens later — seeder ensures the
///     demo restaurant has working defaults out of the box.
/// </summary>
public sealed class LookupSeeder(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<LookupSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IDateTimeProvider clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        DateTime now = clock.UtcNow;

        await SeedShiftsAsync(db, now, ct);
        await SeedCountersAsync(db, now, ct);
        await SeedAreasAsync(db, now, ct);
        await SeedTablesAsync(db, now, ct);
        await SeedKitchenStationsAsync(db, now, ct);
        await SeedUomsAsync(db, now, ct);
        await SeedDenominationsAsync(db, now, ct);
        await SeedCategoriesAsync(db, now, ct);
        await SeedItemsAsync(db, now, ct);

        logger.LogInformation("LookupSeeder finished.");
    }

    private static async Task SeedShiftsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var shifts = new (string Code, string Name, TimeOnly Begin, TimeOnly End, bool IsNextDay)[]
        {
            ("MORN", "Ca Sáng", new TimeOnly(6, 0), new TimeOnly(14, 0), false),
            ("AFTER", "Ca Chiều", new TimeOnly(14, 0), new TimeOnly(22, 0), false),
            ("NIGHT", "Ca Đêm", new TimeOnly(22, 0), new TimeOnly(6, 0), true)
        };
        var existing = (await db.Shifts.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        foreach ((string Code, string Name, TimeOnly Begin, TimeOnly End, bool IsNextDay) s in shifts)
        {
            if (existing.Contains(s.Code))
            {
                continue;
            }

            db.Shifts.Add(new Shift
            {
                Code = s.Code,
                Name = s.Name,
                BeginTime = s.Begin,
                EndTime = s.End,
                IsNextDay = s.IsNextDay,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCountersAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Counters.AnyAsync(ct))
        {
            return;
        }

        db.Counters.Add(new Counter
        {
            Name = "Quầy Trung Tâm",
            Note = "Quầy mặc định seed bởi LookupSeeder",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAreasAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Areas.AnyAsync(ct))
        {
            return;
        }

        Counter? counter = await db.Counters.FirstOrDefaultAsync(ct);
        if (counter is null)
        {
            return;
        }

        db.Areas.AddRange(
            new Area
            {
                CounterId = counter.Id,
                Name = "Sảnh chính",
                Description = "Khu vực bàn thường ở tầng 1",
                DisplayOrder = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Area
            {
                CounterId = counter.Id,
                Name = "Khu VIP",
                Description = "Phụ thu 5% phục vụ + 8% VAT trên phí",
                DisplayOrder = 2,
                ServiceChargePercent = 5m,
                ServiceChargeVatPercent = 8m,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedTablesAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Tables.AnyAsync(ct))
        {
            return;
        }

        List<Area> areas = await db.Areas.OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        if (areas.Count == 0)
        {
            return;
        }

        Area sanh = areas[0];
        Area vip = areas.Count > 1 ? areas[1] : areas[0];

        db.Tables.AddRange(
            new Table
            {
                AreaId = sanh.Id,
                Code = "T01",
                SeatCount = 4,
                Description = "Bàn cạnh cửa sổ",
                Status = TableStatus.Available,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Table
            {
                AreaId = sanh.Id,
                Code = "T02",
                SeatCount = 4,
                Description = null,
                Status = TableStatus.Available,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Table
            {
                AreaId = sanh.Id,
                Code = "T03",
                SeatCount = 6,
                Description = "Bàn dài 6 chỗ",
                Status = TableStatus.Available,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Table
            {
                AreaId = vip.Id,
                Code = "VIP1",
                SeatCount = 8,
                Description = "VIP cửa kính",
                Status = TableStatus.Available,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Table
            {
                AreaId = vip.Id,
                Code = "VIP2",
                SeatCount = 10,
                Description = null,
                Status = TableStatus.Available,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedUomsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var rows = new (string Code, string Name, string? Description)[]
        {
            ("phan", "Phần", "Đơn vị bán theo phần (1 phần cơm, 1 phần combo)"),
            ("to", "Tô", "Đơn vị bán theo tô / bát"),
            ("dia", "Đĩa", "Đơn vị bán theo đĩa"),
            ("chai", "Chai", "Đơn vị bán theo chai (nước ngọt, rượu, ...)"),
            ("lon", "Lon", "Đơn vị bán theo lon"),
            ("ly", "Ly", "Đơn vị bán theo ly / cốc"),
            ("kg", "Kilogam", "Đơn vị khối lượng — gạo, thịt, hải sản tươi"),
            ("g", "Gam", "Đơn vị khối lượng nhỏ — gia vị, rau"),
            ("l", "Lít", "Đơn vị thể tích — bia hơi, nước"),
            ("ml", "Mililit", "Đơn vị thể tích nhỏ — gia vị nước, rượu hộp")
        };

        var existing = (await db.Uoms.Select(x => x.Code.ToLower()).ToListAsync(ct)).ToHashSet();
        foreach ((string Code, string Name, string? Description) u in rows)
        {
            if (existing.Contains(u.Code.ToLower()))
            {
                continue;
            }

            db.Uoms.Add(new Uom
            {
                Code = u.Code,
                Name = u.Name,
                Description = u.Description,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedKitchenStationsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var stations = new (string Code, string Name, short Order)[]
        {
            ("BN", "Bếp Nóng", 0),
            ("BL", "Bếp Lạnh", 1)
        };
        var existing = (await db.KitchenStations.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        foreach ((string Code, string Name, short Order) s in stations)
        {
            if (existing.Contains(s.Code))
            {
                continue;
            }

            db.KitchenStations.Add(new KitchenStation
            {
                Code = s.Code,
                Name = s.Name,
                DisplayOrder = s.Order,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedDenominationsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var denoms = new (decimal Face, string Name)[]
        {
            (500_000m, "500.000đ"),
            (200_000m, "200.000đ"),
            (100_000m, "100.000đ"),
            (50_000m, "50.000đ"),
            (20_000m, "20.000đ"),
            (10_000m, "10.000đ"),
            (5_000m, "5.000đ")
        };
        var existing = (await db.Denominations.Select(x => x.FaceValue).ToListAsync(ct)).ToHashSet();
        short order = 0;
        foreach ((decimal Face, string Name) d in denoms)
        {
            if (existing.Contains(d.Face))
            {
                order++;
                continue;
            }

            db.Denominations.Add(new Denomination
            {
                FaceValue = d.Face,
                Name = d.Name,
                DisplayOrder = order++,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCategoriesAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct))
        {
            return;
        }

        // Per Glossary §3.5: 2 canonical roots — HANG_BAN + NGUYEN_VAT_LIEU.
        // Sample groupings (Đồ uống / Món chính / Món phụ) live UNDER HANG_BAN at level 1;
        // their leaves (Bia / Nước ngọt / Cơm / ...) sit at level 2.
        Category hangBan = await AddCategoryAsync(db, "HANG_BAN", "Hàng bán",
            null, "", 0, 1, now, ct);
        Category nguyenLieu = await AddCategoryAsync(db, "NGUYEN_VAT_LIEU", "Nguyên vật liệu",
            null, "", 0, 2, now, ct);

        var subRoots = new (string Code, string Name, short Order, (string Code, string Name)[] Children)[]
        {
            ("DOUONG", "Đồ uống", 1, new[]
            {
                ("DOUONG_BIA", "Bia"),
                ("DOUONG_NGOT", "Nước ngọt"),
                ("DOUONG_NUOC", "Nước suối")
            }),
            ("MONCHINH", "Món chính", 2, new[]
            {
                ("MC_COM", "Cơm"),
                ("MC_PHO", "Phở / Bún"),
                ("MC_LAU", "Lẩu")
            }),
            ("MONPHU", "Món phụ", 3, new[]
            {
                ("MP_KHAIVI", "Khai vị"),
                ("MP_TRANGMIENG", "Tráng miệng")
            })
        };

        foreach ((string code, string name, short order, (string Code, string Name)[] children) in subRoots)
        {
            Category sub = await AddCategoryAsync(db, code, name,
                hangBan.Id, hangBan.Path,
                1, order, now, ct);

            short leafOrder = 1;
            foreach ((string cCode, string cName) in children)
            {
                await AddCategoryAsync(db, cCode, cName,
                    sub.Id, sub.Path,
                    2, leafOrder++, now, ct);
            }
        }

        // Suppress unused-warning if compiler complains; nguyenLieu is reserved for material seed paths.
        _ = nguyenLieu;
    }

    private static async Task<Category> AddCategoryAsync(
        ApplicationDbContext db, string code, string name,
        int? parentId, string parentPath, short level, short order,
        DateTime now, CancellationToken ct)
    {
        var cat = new Category
        {
            Code = code,
            Name = name,
            ParentId = parentId,
            DisplayOrder = order,
            Level = level,
            Path = "",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Categories.Add(cat);
        await db.SaveChangesAsync(ct);
        cat.Path = $"{parentPath}{cat.Id};";
        await db.SaveChangesAsync(ct);
        return cat;
    }

    private static async Task SeedItemsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Items.AnyAsync(ct))
        {
            return;
        }

        Dictionary<string, int> uomByCode = await db.Uoms.ToDictionaryAsync(u => u.Code, u => u.Id, ct);
        Dictionary<string, int> catByCode = await db.Categories.ToDictionaryAsync(c => c.Code, c => c.Id, ct);
        KitchenStation? hotStation = await db.KitchenStations.FirstOrDefaultAsync(s => s.Code == "BN", ct);
        KitchenStation? coldStation = await db.KitchenStations.FirstOrDefaultAsync(s => s.Code == "BL", ct);

        var seeds = new (string Code, string Name, string Uom, decimal Vat, bool Stockable, bool HasRecipe,
            int? StationId, string PrimaryCategory, string[] SubCategories)[]
            {
                ("BIA_HEINEKEN", "Bia Heineken lon", "lon", 10, true, false, coldStation?.Id, "DOUONG_BIA",
                    Array.Empty<string>()),
                ("BIA_TIGER", "Bia Tiger lon", "lon", 10, true, false, coldStation?.Id, "DOUONG_BIA",
                    Array.Empty<string>()),
                ("COCA_LON", "Coca-Cola lon", "lon", 10, true, false, coldStation?.Id, "DOUONG_NGOT",
                    Array.Empty<string>()),
                ("PEPSI_LON", "Pepsi lon", "lon", 10, true, false, coldStation?.Id, "DOUONG_NGOT",
                    Array.Empty<string>()),
                ("NUOC_LAVIE", "Nước suối Lavie chai", "chai", 10, true, false, coldStation?.Id, "DOUONG_NUOC",
                    Array.Empty<string>()),
                ("COM_GA_XOI_MO", "Cơm gà xối mỡ", "phan", 8, false, true, hotStation?.Id, "MC_COM",
                    Array.Empty<string>()),
                ("COM_SUON_BI", "Cơm sườn bì chả", "phan", 8, false, true, hotStation?.Id, "MC_COM",
                    Array.Empty<string>()),
                ("PHO_BO_TAI", "Phở bò tái", "to", 8, false, true, hotStation?.Id, "MC_PHO", Array.Empty<string>()),
                ("BUN_BO_HUE", "Bún bò Huế", "to", 8, false, true, hotStation?.Id, "MC_PHO", Array.Empty<string>()),
                ("LAU_THAI", "Lẩu Thái", "phan", 8, false, true, hotStation?.Id, "MC_LAU", Array.Empty<string>()),
                ("GOI_CUON", "Gỏi cuốn tôm thịt", "dia", 8, false, true, hotStation?.Id, "MP_KHAIVI",
                    Array.Empty<string>()),
                ("NEM_NUONG", "Nem nướng cuốn bánh", "dia", 8, false, true, hotStation?.Id, "MP_KHAIVI",
                    Array.Empty<string>()),
                ("CHE_KHUC_BACH", "Chè khúc bạch", "ly", 8, false, true, coldStation?.Id, "MP_TRANGMIENG",
                    Array.Empty<string>()),
                ("FLAN", "Bánh flan", "ly", 8, false, true, coldStation?.Id, "MP_TRANGMIENG", Array.Empty<string>()),
                ("THIT_BO", "Thịt bò tươi", "kg", 8, true, false, null, "NGUYEN_VAT_LIEU", Array.Empty<string>())
            };

        foreach ((string Code, string Name, string Uom, decimal Vat, bool Stockable, bool HasRecipe, int? StationId,
                 string PrimaryCategory, string[] SubCategories) s in seeds)
        {
            if (!uomByCode.TryGetValue(s.Uom, out int uomId))
            {
                continue;
            }

            if (!catByCode.TryGetValue(s.PrimaryCategory, out int primaryCatId))
            {
                continue;
            }

            var item = new Item
            {
                Code = s.Code,
                Name = s.Name,
                BaseUomId = uomId,
                VatPercent = s.Vat,
                IsStockable = s.Stockable,
                HasRecipe = s.HasRecipe,
                KitchenStationId = s.StationId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            item.ItemCategories.Add(new ItemCategory
            {
                CategoryId = primaryCatId,
                IsMain = true,
                CreatedAt = now
            });
            foreach (string subCode in s.SubCategories)
            {
                if (catByCode.TryGetValue(subCode, out int subId))
                {
                    item.ItemCategories.Add(new ItemCategory
                    {
                        CategoryId = subId,
                        IsMain = false,
                        CreatedAt = now
                    });
                }
            }

            db.Items.Add(item);
        }

        await db.SaveChangesAsync(ct);
    }
}
