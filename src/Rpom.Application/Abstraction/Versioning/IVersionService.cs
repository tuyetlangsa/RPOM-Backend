namespace Rpom.Application.Abstraction.Versioning;

/// <summary>
///     Atomic counter store used by FE clients to detect cross-aggregate changes
///     via polling. Spec: <c>docs/RPOM_Versioning_Strategy.md</c>.
/// </summary>
public interface IVersionService
{
    /// <summary>
    ///     Atomically increment the version of <paramref name="scope" /> and stamp
    ///     <paramref name="source" /> for audit. Safe against concurrent callers.
    ///     MUST be called AFTER a successful <c>SaveChangesAsync</c> in command
    ///     handlers — never before, or a rolled-back transaction would still bump.
    /// </summary>
    Task BumpAsync(string scope, string source, CancellationToken ct);

    /// <summary>Read current versions for the given scopes; missing scopes return 0.</summary>
    Task<IReadOnlyDictionary<string, long>> GetAsync(IReadOnlyList<string> scopes, CancellationToken ct);
}
