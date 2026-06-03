using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Operations;

/// <summary>
/// Physical printer. Assignment XOR by Type:
/// KITCHEN → KitchenStationId set, CounterId NULL.
/// CASHIER → CounterId set, KitchenStationId NULL.
/// </summary>
public class Printer : Entity
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>KITCHEN | CASHIER (see <see cref="PrinterType"/>).</summary>
    public string Type { get; set; } = null!;

    /// <summary>XOR with CounterId. Set when Type=KITCHEN.</summary>
    public int? KitchenStationId { get; set; }

    /// <summary>XOR with KitchenStationId. Set when Type=CASHIER. FK to Counter (Area B).</summary>
    public int? CounterId { get; set; }

    /// <summary>Printer IP; supports IPv4 (15) + IPv6 (45) max.</summary>
    public string? IpAddress { get; set; }

    /// <summary>OS-level printer name (e.g. Windows printer share name).</summary>
    public string? PrinterName { get; set; }

    /// <summary>Number of copies to print per job.</summary>
    public short PrintCopy { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual KitchenStation? KitchenStation { get; set; }
    public virtual Counter? Counter { get; set; }
}
