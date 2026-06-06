using Rpom.Domain.Common;

namespace Rpom.Domain.Configuration;

public static class RoundingConfigErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "RoundingConfig.NotFound",
        "Không tìm thấy cấu hình làm tròn cho khoá đã chọn.");
}
