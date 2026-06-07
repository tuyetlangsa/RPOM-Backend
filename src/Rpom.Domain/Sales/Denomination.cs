using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

/// <summary>
///     VND cash denomination lookup. Used by CashDrawerCashCount for cashier
///     open/close cash counting. Owner can disable rarely-used denominations.
/// </summary>
public class Denomination : Entity
{
    public int Id { get; set; }

    /// <summary>VND face value: 500000, 200000, 100000, ... Unique.</summary>
    public decimal FaceValue { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Typically sorted desc by FaceValue.</summary>
    public short DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
