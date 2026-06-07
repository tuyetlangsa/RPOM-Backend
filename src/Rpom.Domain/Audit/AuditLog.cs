using Rpom.Domain.Common;

namespace Rpom.Domain.Audit;

/// <summary>
///     Cross-cutting polymorphic audit log. NO foreign keys (intentional).
///     EntityId is polymorphic — refers to a row in the table named in EntityType.
///     ActorStaffAccountId is a soft ref; ActorFullName snapshot keeps history readable
///     even if the StaffAccount is later renamed or deleted.
///     Append-only contract: INSERT only, never UPDATE or DELETE.
/// </summary>
public class AuditLog : Entity
{
    public long Id { get; set; }

    /// <summary>Table name: Ticket, Item, StaffAccount, Reservation, ...</summary>
    public string EntityType { get; set; } = null!;

    /// <summary>Polymorphic — Id of the row in the EntityType table. NO FK.</summary>
    public long EntityId { get; set; }

    /// <summary>CREATE/UPDATE/DELETE + business actions: REOPEN, APPROVE, VOID, SEND, CANCEL, SETTLE, ...</summary>
    public string Action { get; set; } = null!;

    /// <summary>Soft ref to StaffAccount.Id — NO FK. Snapshot ActorFullName preserves history.</summary>
    public int? ActorStaffAccountId { get; set; }

    /// <summary>Snapshot of StaffAccount.FullName at action time.</summary>
    public string? ActorFullName { get; set; }

    public DateTime Timestamp { get; set; }

    /// <summary>Optional human-readable detail (e.g. "Discount applied: VIP 10% (-30,000đ)").</summary>
    public string? Summary { get; set; }
}
