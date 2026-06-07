namespace Rpom.Application.Configuration;

/// <summary>
///     Catalog of all known config codes. Adding a new config:
///     (1) add const here, (2) add seed default in ConfigValueSeeder.
///     Codes use dot-namespace convention <c>{aggregate}.{key}</c>.
/// </summary>
public static class ConfigCodes
{
    // ============ Restaurant profile ============
    public const string RestaurantName = "restaurant.name";
    public const string RestaurantAddress = "restaurant.address";
    public const string RestaurantTaxCode = "restaurant.tax_code";
    public const string RestaurantPhone = "restaurant.phone";
    public const string VatDefaultPercent = "restaurant.vat_default_percent";
    public const string ServiceChargeDefaultPercent = "restaurant.service_charge_default_percent";

    // ============ Reservation hold window ============
    public const string ReservationPreBufferMinutes = "reservation.pre_buffer_minutes";
    public const string ReservationGracePeriodMinutes = "reservation.grace_period_minutes";

    // ============ Kitchen ============
    public const string KitchenLateThresholdMinutes = "kitchen.late_threshold_minutes";

    // ============ Printing ============
    public const string KdsPrintCopiesDefault = "printer.copies_default";
}
