using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Operations;
public class PosTerminal : Entity
{
    public int Id { get; set; }
    public int CounterId { get; set; }
    public string Name { get; set; } = null!;
    public string DeviceToken { get; set; } = null!;
    public DateTime? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Counter Counter { get; set; } = null!;
}
