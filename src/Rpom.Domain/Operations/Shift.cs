using Rpom.Domain.Common;

namespace Rpom.Domain.Operations;

/// <summary>
/// Work shift definition. Referenced by ShiftSession + Ticket (Area E) for
/// revenue ownership, and by ScheduleAssignment (Area G) for staff scheduling.
/// </summary>
public class Shift : Entity
{
    public int Id { get; set; }

    /// <summary>Owner-defined: S_MORNING, S_AFTERNOON, S_NIGHT.</summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>Daily start time (e.g. 06:00).</summary>
    public TimeOnly BeginTime { get; set; }

    /// <summary>Daily end time. If &lt; BeginTime, IsNextDay must be true.</summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>true when EndTime is on the next calendar day (e.g. night shift 22:00 → 06:00).</summary>
    public bool IsNextDay { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
