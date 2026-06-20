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

        var defaults = new (string Code, string? Value, string ValueType, string Description)[]
        {
            // ----- Restaurant profile -----
            (ConfigCodes.RestaurantName, "RPOM Demo", ConfigValueType.Text, "Tên nhà hàng hiển thị trên bill"),
            (ConfigCodes.RestaurantAddress, "Hồ Chí Minh", ConfigValueType.Text, "Địa chỉ hiển thị trên bill"),
            (ConfigCodes.RestaurantTaxCode, "", ConfigValueType.Text, "Mã số thuế nhà hàng (VAT invoice)"),
            (ConfigCodes.RestaurantPhone, "", ConfigValueType.Text, "SĐT nhà hàng"),
            (ConfigCodes.VatDefaultPercent, "10.00", ConfigValueType.Number, "VAT mặc định (%); Ticket snapshot lúc thanh toán"),
            (ConfigCodes.ServiceChargeDefaultPercent, "5.00", ConfigValueType.Number, "Service charge mặc định (%)"),

            // ----- Reservation hold window -----
            (ConfigCodes.ReservationPreBufferMinutes, "30", ConfigValueType.Number, "Pre-buffer hold window (phút) trước TargetTime"),
            (ConfigCodes.ReservationGracePeriodMinutes, "30", ConfigValueType.Number, "Grace period hold window (phút) sau TargetTime"),

            // ----- Kitchen -----
            (ConfigCodes.KitchenLateThresholdMinutes, "15", ConfigValueType.Number,
                "Ngưỡng (phút) mà PROCESSING dish được hiển thị LATE trên KDS"),

            // ----- Customer display + QR -----
            (ConfigCodes.CustomerDisplayIdleMediaUrl, "", ConfigValueType.Text,
                "URL ảnh/video idle global cho màn hình khách (fallback khi display chưa set riêng)."),
            (ConfigCodes.PaymentQrTtlSeconds, "300", ConfigValueType.Number,
                "TTL (giây) của QR PENDING; quá hạn màn khách ngừng hiển thị + tự huỷ. 0 = không hết hạn."),

            // ----- Printing -----
            (ConfigCodes.KdsPrintCopiesDefault, "1", ConfigValueType.Number, "Số bản in mặc định cho printer"),

            // ----- Table operation lock -----
            (ConfigCodes.TableLockTtlSeconds, "60", ConfigValueType.Number,
                "Thời gian giữ lock bàn (giây) sau heartbeat cuối"),

            // ----- Pagination -----
            (ConfigCodes.PaginationMaxPageSize, "500", ConfigValueType.Number,
                "Số bản ghi tối đa cho mỗi trang khi list"),

            // ----- Transfer table -----
            (ConfigCodes.TransferUseTargetAreaServiceCharge, "true", ConfigValueType.Bool,
                "Khi chuyển bàn sang khu khác: true = dùng service charge của khu đích; false = giữ service charge của phiếu.")
        };

        var existing = (await db.ConfigValues.Select(x => x.Code).ToListAsync(ct)).ToHashSet();
        int added = 0;
        foreach ((string Code, string? Value, string ValueType, string Description) d in defaults)
        {
            if (existing.Contains(d.Code))
            {
                continue;
            }

            db.ConfigValues.Add(new ConfigValue
            {
                Code = d.Code,
                Value = d.Value,
                ValueType = d.ValueType,
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
