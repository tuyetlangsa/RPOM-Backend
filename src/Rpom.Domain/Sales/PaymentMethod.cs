using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

/// <summary>
///     Payment method lookup. v1 seeds: CASH, QR (Card defer v2).
///     Vendor configuration (QR provider account, etc.) handled at app layer.
/// </summary>
public class PaymentMethod : Entity
{
    public int Id { get; set; }

    /// <summary>CASH | QR | CARD (future).</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public short DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
