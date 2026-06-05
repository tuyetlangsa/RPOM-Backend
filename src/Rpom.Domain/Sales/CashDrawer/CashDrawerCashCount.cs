using Rpom.Domain.Common;

namespace Rpom.Domain.Sales.CashDrawer;

/// <summary>
/// Cash count breakdown by denomination, per phase (OPENING vs CLOSING).
/// Each session has up to 2N rows (N = active Denominations). Subtotal is
/// a snapshot — survives later changes to Denomination.FaceValue.
/// Unique (CashDrawerSessionId, Phase, DenominationId).
/// </summary>
public class CashDrawerCashCount : Entity
{
    public int Id { get; set; }
    public long CashDrawerSessionId { get; set; }
    public int DenominationId { get; set; }

    /// <summary>OPENING | CLOSING (see <see cref="CashDrawerCashPhase"/>).</summary>
    public string Phase { get; set; } = null!;

    /// <summary>Số tờ của mệnh giá này.</summary>
    public int Quantity { get; set; }

    /// <summary>Snapshot: Quantity × Denomination.FaceValue at count time.</summary>
    public decimal Subtotal { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual CashDrawerSession CashDrawerSession { get; set; } = null!;
    public virtual Denomination Denomination { get; set; } = null!;
}
