using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Infrastructure.Database.Seeding;

/// <summary>
///     Idempotent seeder for report demo data. Creates consistent realistic
///     restaurant data spanning 7 days so all report APIs return meaningful results.
///     Safe to run multiple times — checks existence before inserting.
/// </summary>
public sealed class ReportDemoSeeder(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ReportDemoSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTime now = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1); // end of today UTC

        // Check if already seeded
        if (await db.TicketInvoices.AnyAsync(ct))
        {
            logger.LogInformation("ReportDemoSeeder: data already exists, skipping.");
            return;
        }

        // ─── Step 1: Master Data ───────────────────────────────────────────
        var counters = await SeedCountersAsync(db, now, ct);
        var areas = await SeedAreasAsync(db, counters, now, ct);
        var tables = await SeedTablesAsync(db, areas, now, ct);
        var shifts = await SeedShiftsAsync(db, now, ct);
        var uoms = await SeedUomsAsync(db, now, ct);
        var categories = await SeedCategoriesAsync(db, now, ct);
        var items = await SeedItemsAsync(db, uoms, categories, now, ct);
        var payMethods = await SeedPaymentMethodsAsync(db, now, ct);
        var staff = await SeedStaffAccountsAsync(db, now, ct);

        // ─── Step 2: Operational Data (7 days) ──────────────────────────────
        var random = new Random(42); // fixed seed for reproducibility

        foreach (var counter in counters.Values)
        {
            for (int dayOffset = 6; dayOffset >= 0; dayOffset--)
            {
                DateTime day = now.Date.AddDays(-dayOffset);
                await SeedDayDataAsync(db, counter, areas, tables, shifts, items, payMethods, staff, day, random, now, ct);
            }
        }

        logger.LogInformation("ReportDemoSeeder finished — {TicketCount} tickets seeded.",
            await db.Tickets.CountAsync(ct));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Master Data Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static async Task<Dictionary<string, Counter>> SeedCountersAsync(
        ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var map = new Dictionary<string, Counter>();
        foreach (var (name, code) in new[] { ("Quầy Tầng 1", "T1"), ("Quầy Tầng 2", "T2") })
        {
            var c = await db.Counters.FirstOrDefaultAsync(x => x.Name == name, ct)
                    ?? db.Counters.Add(new Counter { Name = name, IsActive = true, CreatedAt = now, UpdatedAt = now }).Entity;
            map[code] = c;
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, Area>> SeedAreasAsync(
        ApplicationDbContext db, Dictionary<string, Counter> counters, DateTime now, CancellationToken ct)
    {
        var map = new Dictionary<string, Area>();
        var specs = new[] {
            ("Khu VIP Tầng 1", "VIP_T1", "T1"), ("Khu Thường Tầng 1", "THG_T1", "T1"),
            ("Khu VIP Tầng 2", "VIP_T2", "T2"), ("Khu Thường Tầng 2", "THG_T2", "T2")
        };
        foreach (var (name, code, counterKey) in specs)
        {
            var c = counters[counterKey];
            var a = await db.Areas.FirstOrDefaultAsync(x => x.Name == name, ct)
                    ?? db.Areas.Add(new Area { Name = name, CounterId = c.Id, ServiceChargePercent = code.StartsWith("VIP") ? 8m : 5m, IsActive = true, CreatedAt = now, UpdatedAt = now }).Entity;
            map[code] = a;
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, Table>> SeedTablesAsync(
        ApplicationDbContext db, Dictionary<string, Area> areas, DateTime now, CancellationToken ct)
    {
        var map = new Dictionary<string, Table>();
        foreach (var (areaCode, area) in areas)
        {
            for (int i = 1; i <= 4; i++)
            {
                string code = $"{areaCode}-B{i:D2}";
                var t = await db.Tables.FirstOrDefaultAsync(x => x.Code == code, ct)
                        ?? db.Tables.Add(new Table { Code = code, AreaId = area.Id, SeatCount = 4, Status = "AVAILABLE", CreatedAt = now, UpdatedAt = now }).Entity;
                map[code] = t;
            }
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, Shift>> SeedShiftsAsync(
        ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var map = new Dictionary<string, Shift>();
        var specs = new[] {
            ("CA_SANG", "Ca sáng", 7, 0, 15, 0),
            ("CA_CHIEU", "Ca chiều", 15, 0, 23, 0)
        };
        foreach (var (code, name, sh, sm, eh, em) in specs)
        {
            var s = await db.Shifts.FirstOrDefaultAsync(x => x.Code == code, ct)
                    ?? db.Shifts.Add(new Shift { Code = code, Name = name, BeginTime = new TimeOnly(sh, sm), EndTime = new TimeOnly(eh, em), IsActive = true, CreatedAt = now, UpdatedAt = now }).Entity;
            map[code] = s;
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, Uom>> SeedUomsAsync(
        ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var map = new Dictionary<string, Uom>();
        foreach (var (code, name) in new[] { ("BAT", "Bát"), ("DIA", "Đĩa"), ("LY", "Ly"), ("LON", "Lon"), ("CHAI", "Chai") })
        {
            var u = await db.Uoms.FirstOrDefaultAsync(x => x.Code == code, ct)
                    ?? db.Uoms.Add(new Uom { Code = code, Name = name, CreatedAt = now, UpdatedAt = now }).Entity;
            map[code] = u;
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, Category>> SeedCategoriesAsync(
        ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var map = new Dictionary<string, Category>();
        var specs = new[] { ("MON_CHINH", "Món chính"), ("DO_UONG", "Đồ uống"), ("TRANG_MIENG", "Tráng miệng"), ("KHAI_VI", "Khai vị") };
        int order = 0;
        foreach (var (code, name) in specs)
        {
            order++;
            var cat = await db.Categories.FirstOrDefaultAsync(x => x.Code == code, ct)
                      ?? db.Categories.Add(new Category
                      {
                          Code = code, Name = name,
                          Path = order.ToString(),
                          Level = 1,
                          DisplayOrder = (short)order,
                          CreatedAt = now, UpdatedAt = now
                      }).Entity;
            map[code] = cat;
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, Item>> SeedItemsAsync(
        ApplicationDbContext db, Dictionary<string, Uom> uoms, Dictionary<string, Category> categories, DateTime now, CancellationToken ct)
    {
        var map = new Dictionary<string, Item>();
        var specs = new (string Code, string Name, string Uom, string Cat, decimal Price, bool Stock)[]
        {
            ("PHO_BO", "Phở bò tái", "BAT", "MON_CHINH", 100000, true),
            ("COM_GA", "Cơm gà xối mỡ", "DIA", "MON_CHINH", 50000, true),
            ("BUN_CHA", "Bún chả Hà Nội", "DIA", "MON_CHINH", 70000, true),
            ("NEM_RAN", "Nem rán", "DIA", "KHAI_VI", 40000, true),
            ("TRA_DA", "Trà đá", "LY", "DO_UONG", 10000, false),
            ("COCA", "Coca Cola", "LON", "DO_UONG", 25000, false),
            ("BIA_SAIGON", "Bia Sài Gòn", "CHAI", "DO_UONG", 30000, false),
            ("CHE_BA_MAU", "Chè ba màu", "LY", "TRANG_MIENG", 20000, false)
        };
        foreach (var s in specs)
        {
            var item = await db.Items.FirstOrDefaultAsync(x => x.Code == s.Code, ct)
                       ?? db.Items.Add(new Item
                       {
                           Code = s.Code, Name = s.Name, BaseUomId = uoms[s.Uom].Id,
                           IsActive = true, IsStockable = s.Stock, CreatedAt = now, UpdatedAt = now
                       }).Entity;
            map[s.Code] = item;
        }
        await db.SaveChangesAsync(ct); // save items first to get Ids

        // Link items to categories
        foreach (var s in specs)
        {
            var item = map[s.Code];
            if (!await db.ItemCategories.AnyAsync(ic => ic.ItemId == item.Id && ic.CategoryId == categories[s.Cat].Id, ct))
                db.ItemCategories.Add(new ItemCategory { ItemId = item.Id, CategoryId = categories[s.Cat].Id });
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, PaymentMethod>> SeedPaymentMethodsAsync(
        ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var map = new Dictionary<string, PaymentMethod>();
        foreach (var (code, name) in new[] { ("CASH", "Tiền mặt"), ("QR", "Chuyển khoản QR") })
        {
            var pm = await db.PaymentMethods.FirstOrDefaultAsync(x => x.Code == code, ct)
                     ?? db.PaymentMethods.Add(new PaymentMethod { Code = code, Name = name, IsActive = true, CreatedAt = now, UpdatedAt = now }).Entity;
            map[code] = pm;
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, StaffAccount>> SeedStaffAccountsAsync(
        ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var map = new Dictionary<string, StaffAccount>();
        var roleIds = await db.Roles.ToDictionaryAsync(r => r.Code, r => r.Id, ct);
        int managerRoleId = roleIds.GetValueOrDefault("MANAGER", roleIds.Values.First());

        foreach (var (username, fullName) in new[] {
            ("manager", "Nguyễn Quản Lý"), ("thungan1", "Trần Thu Ngân"),
            ("phucvu1", "Lê Phục Vụ"), ("phucvu2", "Phạm Văn Bếp") })
        {
            var staff = await db.StaffAccounts.FirstOrDefaultAsync(x => x.Username == username, ct);
            if (staff == null)
            {
                staff = new StaffAccount
                {
                    Username = username, FullName = fullName,
                    PasswordHash = "$2a$11$dummyhashfordemoseederonly", // "123"
                    RoleId = managerRoleId, IsActive = true,
                    CreatedAt = now, UpdatedAt = now
                };
                db.StaffAccounts.Add(staff);
            }
            map[username] = staff;
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Daily Data Generator
    // ═══════════════════════════════════════════════════════════════════════

    private static async Task SeedDayDataAsync(
        ApplicationDbContext db, Counter counter,
        Dictionary<string, Area> areas, Dictionary<string, Table> tables,
        Dictionary<string, Shift> shifts, Dictionary<string, Item> items,
        Dictionary<string, PaymentMethod> payMethods, Dictionary<string, StaffAccount> staff,
        DateTime day, Random rng, DateTime now, CancellationToken ct)
    {
        var shift = shifts["CA_SANG"]; // all tickets on morning shift for simplicity
        var areaKeys = counter.Name.Contains("Tầng 1") ? new[] { "VIP_T1", "THG_T1" } : new[] { "VIP_T2", "THG_T2" };
        var itemList = items.Values.ToList();
        var waiter = staff.Values.Skip(rng.Next(staff.Count)).First();
        var cashier = staff["thungan1"];
        int billCount = rng.Next(4, 9); // 4-8 bills per day per counter

        // One cash drawer session per day per counter
        var drawer = new CashDrawerSession
        {
            CounterId = counter.Id, ShiftId = shift.Id,
            OpenedByStaffAccountId = cashier.Id, OpenedAt = day.AddHours(7),
            OpeningCash = 2000000, Status = CashDrawerStatus.Closed,
            ClosedByStaffAccountId = cashier.Id, ClosedAt = day.AddHours(15),
            ActualClosingCash = 2000000 + (decimal)(rng.NextDouble() * 5000000 + 3000000),
            ExpectedClosingCash = 2000000 + (decimal)(rng.NextDouble() * 5000000 + 3000000),
            CreatedAt = now, UpdatedAt = now
        };
        drawer.Variance = drawer.ActualClosingCash!.Value - drawer.ExpectedClosingCash!.Value;
        _ = db.CashDrawerSessions.Add(drawer);
        await db.SaveChangesAsync(ct);

        for (int b = 0; b < billCount; b++)
        {
            var area = areas[areaKeys[rng.Next(areaKeys.Length)]];
            var tableList = tables.Values.Where(t => t.AreaId == area.Id).ToList();
            var table = tableList[rng.Next(tableList.Count)];
            short guests = (short)rng.Next(1, 5);
            DateTime openTime = day.AddHours(7 + rng.NextDouble() * 7); // 7:00-14:00
            DateTime closeTime = openTime.AddMinutes(30 + rng.NextDouble() * 60); // 30-90 min

            var ticket = new Ticket
            {
                Code = $"T-{day:yyyyMMdd}-{counter.Name.Replace(" ", "")}-{b + 1:D2}",
                CounterId = counter.Id, AreaId = area.Id, TableId = table.Id,
                CashDrawerSessionId = drawer.Id, ShiftId = shift.Id,
                Status = TicketStatus.Closed, GuestCount = guests,
                WaiterStaffId = waiter.Id,
                OpenedAt = openTime, ClosedAt = closeTime,
                CreatedAt = now, UpdatedAt = now
            };

            // Generate 1-5 items per ticket
            int itemCount = rng.Next(1, 6);
            decimal subtotal = 0;
            var ticketLines = new List<(Item Item, int Qty, decimal Price)>();

            for (int i = 0; i < itemCount; i++)
            {
                var item = itemList[rng.Next(itemList.Count)];
                int qty = rng.Next(1, 4);
                decimal price = item.Code switch
                {
                    "PHO_BO" => 100000, "COM_GA" => 50000, "BUN_CHA" => 70000,
                    "NEM_RAN" => 40000, "TRA_DA" => 10000, "COCA" => 25000,
                    "BIA_SAIGON" => 30000, "CHE_BA_MAU" => 20000, _ => 50000
                };
                ticketLines.Add((item, qty, price));
                subtotal += price * qty;
            }

            bool hasDiscount = rng.NextDouble() < 0.15; // 15% chance discount
            decimal discountAmount = hasDiscount ? Math.Round(subtotal * 0.1m / 1000) * 1000 : 0; // ~10% rounded
            decimal scPercent = area.Name.Contains("VIP") ? 8m : 5m;
            decimal scAmount = Math.Round(subtotal * scPercent / 100 / 1000) * 1000;
            decimal vatAmount = Math.Round((subtotal - discountAmount) * 0.08m / 1000) * 1000;
            decimal total = subtotal - discountAmount + scAmount + vatAmount;

            ticket.Subtotal = subtotal;
            ticket.DiscountAmount = discountAmount;
            ticket.DiscountPercent = subtotal > 0 ? discountAmount / subtotal : 0;
            ticket.ServiceChargePercent = scPercent;
            ticket.ServiceChargeAmount = scAmount;
            ticket.VatAmount = vatAmount;
            ticket.TotalAmount = total;
            ticket.PaidAmount = total;
            _ = db.Tickets.Add(ticket);
            await db.SaveChangesAsync(ct);

            // TicketItemSum + TicketInvoice
            int displayOrder = 0;
            var invoiceLines = new List<TicketInvoiceLine>();
            foreach (var (item, qty, price) in ticketLines)
            {
                displayOrder++;
                decimal lineSub = price * qty;
                decimal lineDisc = hasDiscount ? Math.Round(lineSub * 0.1m / 1000) * 1000 : 0;
                decimal lineSc = Math.Round(lineSub * scPercent / 100 / 1000) * 1000;
                decimal lineVat = Math.Round((lineSub - lineDisc) * 0.08m / 1000) * 1000;
                decimal lineTotal = lineSub - lineDisc + lineSc + lineVat;

                invoiceLines.Add(new TicketInvoiceLine
                {
                    ItemId = item.Id, ItemCode = item.Code, ItemName = item.Name,
                    UomCode = item.BaseUom?.Code ?? "EA", UomName = item.BaseUom?.Name ?? "Each",
                    UnitPrice = price, Quantity = qty,
                    VatPercent = 8m, ServiceChargePercent = scPercent,
                    LineSubtotal = lineSub, TotalDiscount = lineDisc,
                    ServiceChargeAmount = lineSc, VatAmount = lineVat,
                    TotalAmount = lineTotal, DisplayOrder = displayOrder, CreatedAt = now
                });
            }

            _ = db.TicketInvoices.Add(new TicketInvoice
            {
                TicketId = ticket.Id, TicketCode = ticket.Code,
                CounterId = counter.Id, AreaId = area.Id, ShiftId = shift.Id,
                TableId = table.Id, TableCode = table.Code,
                GuestCount = guests, WaiterName = waiter.FullName,
                ClosedByName = cashier.FullName,
                Subtotal = subtotal, DiscountAmount = discountAmount,
                DiscountPercent = ticket.DiscountPercent,
                ServiceChargeAmount = scAmount, ServiceChargePercent = scPercent,
                VatAmount = vatAmount, TotalAmount = total,
                PaidAmount = total, OpenedAt = openTime, ClosedAt = closeTime,
                CreatedAt = now, Lines = invoiceLines
            });

            // Payments (mix cash + QR)
            bool useBoth = total > 300000 && rng.NextDouble() < 0.3;
            if (useBoth)
            {
                decimal cashAmt = Math.Round(total * 0.6m / 1000) * 1000;
                _ = db.TicketPaymentDetails.Add(new TicketPaymentDetail
                {
                    TicketId = ticket.Id, PaymentMethodId = payMethods["CASH"].Id,
                    Amount = cashAmt, Status = TicketPaymentStatus.Success,
                    ProcessedByStaffId = cashier.Id, ProcessedAt = closeTime, CreatedAt = now
                });
                _ = db.TicketPaymentDetails.Add(new TicketPaymentDetail
                {
                    TicketId = ticket.Id, PaymentMethodId = payMethods["QR"].Id,
                    Amount = total - cashAmt, Status = TicketPaymentStatus.Success,
                    ProcessedByStaffId = cashier.Id, ProcessedAt = closeTime, CreatedAt = now
                });
            }
            else
            {
                string payMethod = rng.NextDouble() < 0.6 ? "CASH" : "QR";
                _ = db.TicketPaymentDetails.Add(new TicketPaymentDetail
                {
                    TicketId = ticket.Id, PaymentMethodId = payMethods[payMethod].Id,
                    Amount = total, Status = TicketPaymentStatus.Success,
                    ProcessedByStaffId = cashier.Id, ProcessedAt = closeTime, CreatedAt = now
                });
            }

            // Audit log
            _ = db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Ticket", EntityId = ticket.Id,
                Action = "CLOSE", ActorFullName = cashier.FullName,
                Timestamp = closeTime, Summary = $"Đóng phiếu {ticket.Code} — {total:N0}đ"
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
