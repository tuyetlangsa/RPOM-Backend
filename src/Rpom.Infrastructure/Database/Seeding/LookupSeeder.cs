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
        await SeedPaymentMethodsAsync(db, now, ct);
        await SeedCancellationReasonsAsync(db, now, ct);
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
            ("con", "Con", "Đơn vị bán theo con (cua, ghẹ, cá nguyên con)"),
            ("suat", "Suất", "Đơn vị bán theo suất (combo, set menu)"),
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

    private static async Task SeedCancellationReasonsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        // Shared by OrderItem cancel, OrderItem refund, and Ticket cancel. "OTHER" is the
        // catch-all with an optional free-text note.
        var reasons = new (string Code, string Name, short Order)[]
        {
            ("CUS_CHANGE_MIND", "Khách đổi ý", 1),
            ("OUT_OF_STOCK", "Hết hàng", 2),
            ("WRONG_DISH", "Lên sai món", 3),
            ("FOREIGN_OBJECT", "Có dị vật", 4),
            ("QUALITY", "Chất lượng không đạt", 5),
            ("OTHER", "Lý do khác", 6)
        };
        var existing = (await db.CancellationReasons.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        foreach ((string Code, string Name, short Order) r in reasons)
        {
            if (existing.Contains(r.Code))
            {
                continue;
            }

            db.CancellationReasons.Add(new CancellationReason
            {
                Code = r.Code,
                Name = r.Name,
                DisplayOrder = r.Order,
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

    private static async Task SeedPaymentMethodsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var methods = new (string Code, string Name, string Description, short Order)[]
        {
            (PaymentMethodCodes.Cash, "Tiền mặt", "Thanh toán bằng tiền mặt tại quầy", 0),
            (PaymentMethodCodes.Qr,   "QR - SePay", "Thanh toán chuyển khoản QR qua SePay", 1),
        };
        var existing = (await db.PaymentMethods.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        foreach (var m in methods)
        {
            if (existing.Contains(m.Code)) continue;
            db.PaymentMethods.Add(new PaymentMethod
            {
                Code = m.Code,
                Name = m.Name,
                Description = m.Description,
                DisplayOrder = m.Order,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCancellationReasonsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var reasons = new (string Code, string Name, short Order)[]
        {
            ("CUS_CHANGE_MIND", "Khách đổi ý",          0),
            ("OUT_OF_STOCK",    "Hết hàng",             1),
            ("WRONG_DISH",      "Sai món",              2),
            ("FOREIGN_OBJECT",  "Dị vật trong món",     3),
            ("QUALITY",         "Chất lượng không đạt", 4),
            ("MERGE",           "Gộp hoá đơn",          5),
            ("OTHER",           "Lý do khác",           6),
        };
        var existing = (await db.CancellationReasons.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        foreach (var r in reasons)
        {
            if (existing.Contains(r.Code)) continue;
            db.CancellationReasons.Add(new CancellationReason
            {
                Code = r.Code,
                Name = r.Name,
                DisplayOrder = r.Order,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
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
            ("KHAIVI", "Khai vị", 1, new[]
            {
                ("KV_GOI", "Gỏi & Nộm"),
                ("KV_CHIEN", "Món chiên"),
                ("KV_CUON", "Cuốn & Khai vị nguội")
            }),
            ("MONCHINH", "Món chính", 2, new[]
            {
                ("MC_COM", "Cơm"),
                ("MC_PHO", "Phở & Bún"),
                ("MC_MI", "Mì & Miến")
            }),
            ("LAUNUONG", "Lẩu & Nướng", 3, new[]
            {
                ("LN_LAU", "Lẩu"),
                ("LN_NUONG", "Nướng / BBQ")
            }),
            ("HAISAN", "Hải sản", 4, new[]
            {
                ("HS_TOM", "Tôm"),
                ("HS_CUA", "Cua & Ghẹ"),
                ("HS_CA", "Cá & Mực")
            }),
            ("MONCHAY", "Món chay", 5, new[]
            {
                ("MCH_CHAY", "Món chay")
            }),
            ("TRANGMIENG", "Tráng miệng", 6, new[]
            {
                ("TM_CHE", "Chè"),
                ("TM_BANH", "Bánh & Kem")
            }),
            ("DOUONG", "Đồ uống", 7, new[]
            {
                ("DOUONG_BIA", "Bia"),
                ("DOUONG_NGOT", "Nước ngọt & Nước suối"),
                ("DU_CAPHE", "Cà phê"),
                ("DU_TRA", "Trà & Sinh tố")
            }),
            ("COMBO", "Combo & Set menu", 8, new[]
            {
                ("CB_SET", "Combo")
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
        int? hot = (await db.KitchenStations.FirstOrDefaultAsync(s => s.Code == "BN", ct))?.Id;
        int? cold = (await db.KitchenStations.FirstOrDefaultAsync(s => s.Code == "BL", ct))?.Id;

        // Per-leaf menu groups: (leaf category code, UOM code, VAT%, kitchen station, dish names).
        // Item codes are generated as {leafCode}_{index}; combos are seeded separately by CashierDemoSeeder.
        var groups = new (string Cat, string Uom, decimal Vat, int? Station, string[] Names)[]
        {
            ("KV_GOI", "dia", 8, hot, new[]
            {
                "Gỏi gà bắp cải", "Gỏi ngó sen tôm thịt", "Gỏi xoài khô bò", "Gỏi đu đủ tôm thịt",
                "Nộm sứa hoa chuối", "Gỏi bò bóp thấu", "Gỏi cá trích", "Nộm rau muống tai heo",
                "Gỏi mực chua cay", "Gỏi tôm càng", "Nộm bò khô đu đủ", "Gỏi gà xé phay"
            }),
            ("KV_CHIEN", "dia", 8, hot, new[]
            {
                "Chả giò rế", "Khoai tây chiên", "Nem chua rán", "Cánh gà chiên mắm",
                "Mực chiên giòn", "Tôm chiên xù", "Đậu hũ chiên sả", "Chả cá chiên",
                "Hành tây chiên giòn", "Bánh tôm chiên", "Phô mai que", "Gà popcorn",
                "Cá viên chiên", "Bạch tuộc chiên giòn"
            }),
            ("KV_CUON", "dia", 8, cold, new[]
            {
                "Gỏi cuốn tôm thịt", "Bò bía", "Nem nướng cuốn bánh", "Bánh tráng cuốn thịt heo",
                "Cuốn diếp cá tôm", "Phở cuốn", "Bì cuốn", "Cuốn cá hồi rong biển",
                "Bánh ướt cuốn", "Cuốn tôm chấy"
            }),
            ("MC_COM", "phan", 8, hot, new[]
            {
                "Cơm gà xối mỡ", "Cơm sườn bì chả", "Cơm tấm sườn nướng", "Cơm chiên dương châu",
                "Cơm chiên hải sản", "Cơm gà Hải Nam", "Cơm bò lúc lắc", "Cơm cá kho tộ",
                "Cơm gà Singapore", "Cơm chiên cá mặn", "Cơm thịt kho trứng", "Cơm gà teriyaki",
                "Cơm sườn Hàn Quốc", "Cơm tôm rim", "Cơm gà nướng mật ong", "Cơm chiên trứng muối",
                "Cơm bò xào cải", "Cơm gà sốt nấm", "Cơm sườn ram mặn", "Cơm cá hồi áp chảo"
            }),
            ("MC_PHO", "to", 8, hot, new[]
            {
                "Phở bò tái", "Phở bò chín", "Phở bò tái nạm", "Phở gà",
                "Phở sốt vang", "Bún bò Huế", "Bún chả Hà Nội", "Bún riêu cua",
                "Bún mọc", "Bún thịt nướng", "Bún cá rô đồng", "Hủ tiếu Nam Vang",
                "Hủ tiếu bò kho", "Bún măng vịt", "Phở trộn", "Bún đậu mắm tôm",
                "Bún ốc", "Phở khô Gia Lai"
            }),
            ("MC_MI", "to", 8, hot, new[]
            {
                "Mì xào bò", "Mì xào hải sản", "Mì Quảng", "Miến xào cua",
                "Mì vịt tiềm", "Mì hoành thánh", "Miến gà", "Mì xào giòn thập cẩm",
                "Mì cay Hàn Quốc", "Miến lươn", "Mì trộn Indomie", "Mì udon bò"
            }),
            ("LN_LAU", "phan", 8, hot, new[]
            {
                "Lẩu Thái tôm yum", "Lẩu gà lá é", "Lẩu hải sản", "Lẩu bò nhúng giấm",
                "Lẩu riêu cua bắp bò", "Lẩu cá kèo", "Lẩu nấm chay", "Lẩu mắm",
                "Lẩu gà ớt hiểm", "Lẩu Tứ Xuyên", "Lẩu cua đồng", "Lẩu dê",
                "Lẩu ếch măng cay", "Lẩu cá lăng"
            }),
            ("LN_NUONG", "phan", 8, hot, new[]
            {
                "Ba chỉ heo nướng", "Bò nướng tảng", "Sườn nướng BBQ", "Gà nướng muối ớt",
                "Mực nướng sa tế", "Tôm nướng muối ớt", "Cá nướng giấy bạc", "Nầm bò nướng",
                "Lòng nướng", "Bạch tuộc nướng", "Chân gà nướng", "Sụn gà nướng",
                "Thịt xiên nướng", "Sò điệp nướng mỡ hành", "Dồi sụn nướng", "Cá hồi nướng",
                "Bò cuộn nấm kim châm", "Tôm sú nướng phô mai", "Heo tộc nướng", "Ức gà nướng"
            }),
            ("HS_TOM", "phan", 8, hot, new[]
            {
                "Tôm hấp bia", "Tôm rang me", "Tôm sú hấp nước dừa", "Tôm cháy tỏi",
                "Tôm hùm nướng phô mai", "Tôm sốt Thái", "Tôm chiên bột", "Tôm rim mặn ngọt",
                "Tôm hấp sả", "Tôm xào rau củ", "Tôm nướng muối ớt xanh", "Tôm sốt bơ tỏi"
            }),
            ("HS_CUA", "con", 8, hot, new[]
            {
                "Cua rang me", "Cua sốt ớt Singapore", "Ghẹ hấp bia", "Cua rang muối",
                "Ghẹ rang me", "Cua hấp", "Càng ghẹ rang muối", "Miến xào cua",
                "Súp cua", "Ghẹ sốt trứng muối"
            }),
            ("HS_CA", "phan", 8, hot, new[]
            {
                "Mực hấp gừng", "Mực xào sa tế", "Cá lăng nướng", "Cá diêu hồng hấp Hong Kong",
                "Cá kho tộ", "Mực một nắng nướng", "Cá basa kho tiêu", "Mực nhồi thịt",
                "Cá thu sốt cà", "Bạch tuộc xào cay", "Cá hồi sốt teriyaki", "Cá chẽm hấp xì dầu",
                "Mực trứng hấp", "Cá tầm nướng", "Cá nục kho", "Mực ống nướng"
            }),
            ("MCH_CHAY", "phan", 8, hot, new[]
            {
                "Đậu hũ sốt nấm", "Rau củ xào thập cẩm", "Cơm chiên chay", "Nấm kho tiêu",
                "Lẩu nấm chay", "Gỏi cuốn chay", "Mì xào chay", "Canh chua chay",
                "Đậu hũ chiên sả", "Chả giò chay", "Bún Huế chay", "Cà tím nướng mỡ hành",
                "Súp rong biển chay", "Cơm tấm chay", "Phở chay", "Nem nướng chay"
            }),
            ("TM_CHE", "ly", 8, cold, new[]
            {
                "Chè khúc bạch", "Chè ba màu", "Chè thái sầu riêng", "Chè đậu xanh",
                "Chè bưởi", "Chè hạt sen", "Chè trôi nước", "Chè chuối nước cốt dừa",
                "Chè khoai môn", "Chè dưỡng nhan", "Sương sáo hạt lựu", "Chè thập cẩm"
            }),
            ("TM_BANH", "phan", 8, cold, new[]
            {
                "Bánh flan", "Rau câu dừa", "Kem dừa", "Kem chiên",
                "Bánh tiramisu", "Bánh mousse chanh dây", "Bánh su kem", "Panna cotta",
                "Bánh chuối nướng", "Kem cốt dừa", "Bánh kẹp lá dứa", "Sữa chua nếp cẩm"
            }),
            ("DOUONG_BIA", "lon", 10, cold, new[]
            {
                "Bia Heineken lon", "Bia Tiger lon", "Bia Saigon Special", "Bia 333",
                "Bia Larue", "Bia Budweiser", "Bia Corona", "Bia Tiger Crystal",
                "Bia Heineken Silver", "Bia Sapporo", "Bia Hà Nội", "Bia Huda"
            }),
            ("DOUONG_NGOT", "lon", 10, cold, new[]
            {
                "Coca-Cola lon", "Pepsi lon", "7Up lon", "Sprite lon",
                "Fanta cam", "Mirinda xá xị", "Nước suối Lavie", "Nước suối Aquafina",
                "Red Bull", "Sting dâu", "Number 1", "C2 trà xanh",
                "Soda chanh", "Nước cam ép"
            }),
            ("DU_CAPHE", "ly", 10, cold, new[]
            {
                "Cà phê đen đá", "Cà phê sữa đá", "Bạc xỉu", "Cà phê muối",
                "Cappuccino", "Latte", "Americano", "Espresso",
                "Cà phê cốt dừa", "Mocha", "Cà phê trứng", "Caramel Macchiato",
                "Cold Brew", "Cà phê đen nóng", "Cà phê sữa nóng", "Affogato"
            }),
            ("DU_TRA", "ly", 10, cold, new[]
            {
                "Trà đào cam sả", "Trà vải", "Trà sữa trân châu", "Trà tắc",
                "Trà chanh", "Trà ô long", "Sinh tố bơ", "Sinh tố xoài",
                "Sinh tố dâu", "Nước ép cà rốt", "Nước ép dứa", "Trà gừng mật ong",
                "Trà hoa cúc", "Soda việt quất", "Sinh tố mãng cầu", "Trà sữa matcha",
                "Nước ép ổi", "Trà đào hạt chia"
            })
        };

        int seq = 1;
        foreach ((string cat, string uom, decimal vat, int? station, string[] names) in groups)
        {
            if (!uomByCode.TryGetValue(uom, out int uomId) || !catByCode.TryGetValue(cat, out int catId))
            {
                continue;
            }

            foreach (string name in names)
            {
                bool isDrink = cat is "DOUONG_BIA" or "DOUONG_NGOT";
                var item = new Item
                {
                    Code = $"{cat}_{seq:D3}",
                    Name = name,
                    BaseUomId = uomId,
                    VatPercent = vat,
                    IsStockable = isDrink,         // bottled/canned drinks are stock-tracked
                    HasRecipe = !isDrink,          // cooked dishes have recipes
                    KitchenStationId = station,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                item.ItemCategories.Add(new ItemCategory
                {
                    CategoryId = catId,
                    IsMain = true,
                    CreatedAt = now
                });
                db.Items.Add(item);
                seq++;
            }
        }

        // A couple of raw materials under NGUYEN_VAT_LIEU for stock demos.
        if (catByCode.TryGetValue("NGUYEN_VAT_LIEU", out int nvlId) && uomByCode.TryGetValue("kg", out int kgId))
        {
            foreach ((string code, string name) in new[]
                     {
                         ("NVL_THIT_BO", "Thịt bò tươi"), ("NVL_TOM", "Tôm sú tươi"),
                         ("NVL_RAU", "Rau ăn lẩu")
                     })
            {
                var raw = new Item
                {
                    Code = code,
                    Name = name,
                    BaseUomId = kgId,
                    VatPercent = 8m,
                    IsStockable = true,
                    HasRecipe = false,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                raw.ItemCategories.Add(new ItemCategory { CategoryId = nvlId, IsMain = true, CreatedAt = now });
                db.Items.Add(raw);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
