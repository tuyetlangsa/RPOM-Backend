using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rpom.Application.Abstraction.Clock;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Seeding;

/// <summary>
/// Idempotent seeder for minimal lookup + master data needed to smoke-test
/// ShiftSession and the Restaurant master-data screens (Counter / Area / Table):
///   - 3 Shifts (MORN, AFTER, NIGHT)
///   - 1 Counter (Quầy Trung Tâm), 2 sample Areas, 5 sample Tables
///   - 2 KitchenStations (Bếp Nóng, Bếp Lạnh)
///   - 7 VND Denominations
///
/// Owner manages these via Master Data screens later — seeder ensures the
/// demo restaurant has working defaults out of the box.
/// </summary>
public sealed class LookupSeeder(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<LookupSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = clock.UtcNow;

        await SeedShiftsAsync(db, now, ct);
        await SeedCountersAsync(db, now, ct);
        await SeedAreasAsync(db, now, ct);
        await SeedTablesAsync(db, now, ct);
        await SeedKitchenStationsAsync(db, now, ct);
        await SeedDenominationsAsync(db, now, ct);

        logger.LogInformation("LookupSeeder finished.");
    }

    private static async Task SeedShiftsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var shifts = new (string Code, string Name, TimeOnly Begin, TimeOnly End, bool IsNextDay)[]
        {
            ("MORN",  "Ca Sáng",  new TimeOnly(6,  0), new TimeOnly(14, 0), false),
            ("AFTER", "Ca Chiều", new TimeOnly(14, 0), new TimeOnly(22, 0), false),
            ("NIGHT", "Ca Đêm",   new TimeOnly(22, 0), new TimeOnly(6,  0), true),
        };
        var existing = (await db.Shifts.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        foreach (var s in shifts)
        {
            if (existing.Contains(s.Code)) continue;
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
        if (await db.Counters.AnyAsync(ct)) return;
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
        if (await db.Areas.AnyAsync(ct)) return;

        var counter = await db.Counters.FirstOrDefaultAsync(ct);
        if (counter is null) return;

        db.Areas.AddRange(
            new Area
            {
                CounterId = counter.Id,
                Name = "Sảnh chính",
                Description = "Khu vực bàn thường ở tầng 1",
                DisplayOrder = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new Area
            {
                CounterId = counter.Id,
                Name = "Khu VIP",
                Description = "Phụ thu 15% — phục vụ riêng",
                DisplayOrder = 2,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedTablesAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Tables.AnyAsync(ct)) return;

        var areas = await db.Areas.OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        if (areas.Count == 0) return;

        var sanh = areas[0];
        var vip = areas.Count > 1 ? areas[1] : areas[0];

        db.Tables.AddRange(
            new Table { AreaId = sanh.Id, Code = "T01", SeatCount = 4, Description = "Bàn cạnh cửa sổ", Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Table { AreaId = sanh.Id, Code = "T02", SeatCount = 4, Description = null,             Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Table { AreaId = sanh.Id, Code = "T03", SeatCount = 6, Description = "Bàn dài 6 chỗ",  Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Table { AreaId = vip.Id,  Code = "VIP1", SeatCount = 8, Description = "VIP cửa kính",  Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Table { AreaId = vip.Id,  Code = "VIP2", SeatCount = 10, Description = null,           Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now }
        );
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedKitchenStationsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var stations = new (string Code, string Name, short Order)[]
        {
            ("BN", "Bếp Nóng", 0),
            ("BL", "Bếp Lạnh", 1),
        };
        var existing = (await db.KitchenStations.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        foreach (var s in stations)
        {
            if (existing.Contains(s.Code)) continue;
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
            ( 50_000m,  "50.000đ"),
            ( 20_000m,  "20.000đ"),
            ( 10_000m,  "10.000đ"),
            (  5_000m,   "5.000đ"),
        };
        var existing = (await db.Denominations.Select(x => x.FaceValue).ToListAsync(ct)).ToHashSet();
        short order = 0;
        foreach (var d in denoms)
        {
            if (existing.Contains(d.Face)) { order++; continue; }
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
}
