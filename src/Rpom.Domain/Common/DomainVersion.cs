namespace Rpom.Domain.Common;

/// <summary>
///     Aggregate snapshot version per logical scope. FE clients poll
///     <c>GET /api/sync/versions?scopes=...</c> at a low frequency to detect
///     cross-row changes cheaply.
///     <para>
///         Mutated only by <c>IVersionService.BumpAsync</c> via an atomic UPSERT;
///         Command handlers never write to this table directly.
///     </para>
///     Spec: <c>docs/RPOM_Versioning_Strategy.md</c>.
/// </summary>
public class DomainVersion
{
    /// <summary>Catalog scope key. See <c>VersionScopes</c>.</summary>
    public string Scope { get; set; } = null!;

    /// <summary>Monotonic counter; +1 on each bump.</summary>
    public long Version { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Annotation of the latest bump (e.g. "Uom.Create(id=11)").</summary>
    public string? UpdatedBySource { get; set; }
}
