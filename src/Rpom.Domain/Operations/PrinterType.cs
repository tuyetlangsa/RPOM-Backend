namespace Rpom.Domain.Operations;

/// <summary>Printer.Type discriminator values.</summary>
public static class PrinterType
{
    /// <summary>KITCHEN → KitchenStationId required, CounterId NULL.</summary>
    public const string Kitchen = "KITCHEN";

    /// <summary>CASHIER → CounterId required, KitchenStationId NULL.</summary>
    public const string Cashier = "CASHIER";
}
