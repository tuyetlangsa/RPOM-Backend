using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
///     Tree (self-ref) for item categorization. Purely organizational — item kind
///     (sellable / material) is determined by Item flags, not by walking to a root.
///     v1 seeds 2 roots: "Hàng bán" + "Nguyên vật liệu".
/// </summary>
public class Category : Entity
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Self-ref. NULL for root categories.</summary>
    public int? ParentId { get; set; }

    /// <summary>
    ///     Denormalized tree path of ancestor Ids, semicolon-separated (e.g. "1;5;12").
    ///     Maintained by app/trigger on INSERT or ParentId UPDATE.
    ///     Enables fast descendants query: WHERE Path LIKE '@id;%'.
    /// </summary>
    public string Path { get; set; } = null!;

    /// <summary>Depth in tree; root = 0. Denormalized cache.</summary>
    public short Level { get; set; }

    public short DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Category? Parent { get; set; }
    public virtual ICollection<Category> Children { get; set; } = new List<Category>();
    public virtual ICollection<ItemCategory> ItemCategories { get; set; } = new List<ItemCategory>();
}
