namespace Rpom.Infrastructure.Database.Seeding;

/// <summary>
///     Initial Owner account credentials, used by <see cref="AccessSeeder" /> on first
///     startup. Idempotent — if an account with <see cref="OwnerUsername" /> already
///     exists, seeding is a no-op. Bound from configuration section "Bootstrap".
/// </summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string OwnerUsername { get; set; } = "owner";
    public string OwnerPassword { get; set; } = null!;
    public string OwnerFullName { get; set; } = "Restaurant Owner";
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }
}
