using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Domain.Menu;

namespace Rpom.Application.Categories;

/// <summary>
/// Shared helpers for Category tree maintenance — Path/Level recomputation
/// and cycle detection. Path format: semicolon-separated ancestor ids
/// terminated with trailing ';', e.g. root "1;", child "1;5;", grandchild
/// "1;5;12;". The trailing ';' lets `LIKE 'parentPath%'` query select all
/// descendants (vs. having to OR the parent itself).
/// </summary>
internal static class CategoryTreeHelpers
{
    public const char Sep = ';';

    public static string ComputePath(Category? parent, int selfId) =>
        parent is null
            ? $"{selfId}{Sep}"
            : $"{parent.Path}{selfId}{Sep}";

    public static short ComputeLevel(Category? parent) =>
        parent is null ? (short)0 : (short)(parent.Level + 1);

    /// <summary>
    /// True when promoting <paramref name="candidateId"/> into the subtree of
    /// <paramref name="newParent"/> would create a cycle (newParent is the
    /// candidate itself, or a descendant of it).
    /// </summary>
    public static bool WouldCreateCycle(Category newParent, int candidateId)
    {
        if (newParent.Id == candidateId) return true;
        return newParent.Path.Contains($"{candidateId}{Sep}");
    }

    /// <summary>
    /// Cascade Path + Level for every row whose old Path begins with the
    /// supplied old prefix. Called once when ParentId of a category changes.
    /// Caller must SaveChanges afterwards.
    /// </summary>
    public static async Task RecomputeDescendantsAsync(
        IDbContext dbContext,
        Category node,
        string oldPath,
        short oldLevel,
        CancellationToken ct)
    {
        var newPath = node.Path;
        var newLevel = node.Level;
        if (oldPath == newPath && oldLevel == newLevel) return;

        var descendants = await dbContext.Categories
            .Where(c => c.Id != node.Id
                     && EF.Functions.Like(c.Path, oldPath + "%"))
            .ToListAsync(ct);

        foreach (var d in descendants)
        {
            d.Path = newPath + d.Path[oldPath.Length..];
            d.Level = (short)(newLevel + (d.Level - oldLevel));
        }
    }
}
