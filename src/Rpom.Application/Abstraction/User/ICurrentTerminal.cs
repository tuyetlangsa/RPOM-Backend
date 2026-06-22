namespace Rpom.Application.Abstraction.User;

/// <summary>
///     Per-request accessor for the POS terminal device token, read from the
///     <c>X-Terminal-Token</c> header. Orthogonal to staff auth (JWT) — identifies the MACHINE,
///     not the person. Handlers resolve this to a <c>PosTerminal</c>. NULL when header absent
///     (e.g. cash flow, or a POS not provisioned) — callers treat that as "no terminal".
/// </summary>
public interface ICurrentTerminal
{
    /// <summary>Raw token from the X-Terminal-Token header, or NULL when not present.</summary>
    string? TerminalToken { get; }
}
