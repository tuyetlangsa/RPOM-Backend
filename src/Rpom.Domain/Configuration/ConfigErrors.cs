using Rpom.Domain.Common;

namespace Rpom.Domain.Configuration;

public static class ConfigErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Config.NotFound",
        "Không tìm thấy config code này.");

    public static readonly Error InvalidCode = Error.BadRequest(
        "Config.InvalidCode",
        "Config code không hợp lệ.");
}
