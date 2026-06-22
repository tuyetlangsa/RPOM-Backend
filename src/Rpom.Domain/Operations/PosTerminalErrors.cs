using Rpom.Domain.Common;

namespace Rpom.Domain.Operations;

public static class PosTerminalErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "PosTerminal.NotFound",
        "Không tìm thấy máy POS.");

    public static readonly Error InvalidToken = Error.NotFound(
        "PosTerminal.InvalidToken",
        "Token máy POS không hợp lệ hoặc đã bị vô hiệu.");
}
