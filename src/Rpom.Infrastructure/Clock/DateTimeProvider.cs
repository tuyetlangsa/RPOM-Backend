using Rpom.Application.Abstraction.Clock;

namespace Rpom.Infrastructure.Clock;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
