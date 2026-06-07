using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Configuration;
using Rpom.Domain.Configuration;

namespace Rpom.Infrastructure.Database.Seeding;

/// <summary>
///     Idempotent seeder for ConfigValue defaults. Inserts rows missing from DB;
///     never overwrites rows Owner has edited.
/// </summary>
public sealed class ConfigValueSeeder(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ConfigValueSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IDateTimeProvider clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        DateTime now = clock.UtcNow;

        var defaults = new (string Code, string? Value, string Description)[]
        {
            // ----- Restaurant profile -----
            (ConfigCodes.RestaurantName, "RPOM Demo", "Tên nhà hàng hiển thị trên bill"),
            (ConfigCodes.RestaurantAddress, "Hồ Chí Minh", "Địa chỉ hiển thị trên bill"),
            (ConfigCodes.RestaurantTaxCode, "", "Mã số thuế nhà hàng (VAT invoice)"),
            (ConfigCodes.RestaurantPhone, "", "SĐT nhà hàng"),
            (ConfigCodes.VatDefaultPercent, "10.00", "VAT mặc định (%); Ticket snapshot lúc thanh toán"),
            (ConfigCodes.ServiceChargeDefaultPercent, "5.00", "Service charge mặc định (%)"),

            // ----- Reservation hold window -----
            (ConfigCodes.ReservationPreBufferMinutes, "30", "Pre-buffer hold window (phút) trước TargetTime"),
            (ConfigCodes.ReservationGracePeriodMinutes, "30", "Grace period hold window (phút) sau TargetTime"),

            // ----- Kitchen -----
            (ConfigCodes.KitchenLateThresholdMinutes, "15",
                "Ngưỡng (phút) mà PROCESSING dish được hiển thị LATE trên KDS"),

            // ----- Printing -----
            (ConfigCodes.KdsPrintCopiesDefault, "1", "Số bản in mặc định cho printer")
        };

        var existing = (await db.ConfigValues.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        int added = 0;
        foreach ((string Code, string? Value, string Description) d in defaults)
        {
            if (existing.Contains(d.Code))
            {
                continue;
            }

            db.ConfigValues.Add(new ConfigValue
            {
                Code = d.Code,
                Value = d.Value,
                Description = d.Description,
                UpdatedAt = now,
                UpdatedByStaffAccountId = null
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("ConfigValueSeeder finished — {Added} new rows seeded ({Total} codes in catalog).",
            added, defaults.Length);
    }
}
