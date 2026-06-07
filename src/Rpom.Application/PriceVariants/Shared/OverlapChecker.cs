namespace Rpom.Application.PriceVariants.Shared;

/// <summary>
///     Save-time conflict validator cho PriceVariant. Theo spec
///     <c>docs/RPOM_Pricing_Spec.md</c> §7.2: 2 variant cùng specificity mà overlap
///     cả 3 chiều (Time × Day × Area) → reject.
/// </summary>
public static class OverlapChecker
{
    public static int Specificity(VariantSnapshot v) =>
        (v.BeginTime is not null || v.EndTime is not null ? 1 : 0)
        + (v.DayMask is not null ? 1 : 0)
        + (v.AppliesToAllAreas ? 0 : 1);

    /// <summary>
    ///     Kiểm tra draft xung đột với các sibling. Trả về null nếu OK,
    ///     hoặc variant đầu tiên bị conflict.
    /// </summary>
    public static VariantSnapshot? FindConflict(VariantSnapshot draft, IEnumerable<VariantSnapshot> siblings)
    {
        int draftSpec = Specificity(draft);
        foreach (VariantSnapshot s in siblings)
        {
            if (s.Id == draft.Id)
            {
                continue;
            }

            if (Specificity(s) != draftSpec)
            {
                continue;
            }

            if (TimeOverlap(draft, s) && DayOverlap(draft, s) && AreaOverlap(draft, s))
            {
                return s;
            }
        }

        return null;
    }

    private static bool TimeOverlap(VariantSnapshot a, VariantSnapshot b)
    {
        // NULL ở bất kỳ bên = match all
        bool aAllDay = a.BeginTime is null && a.EndTime is null;
        bool bAllDay = b.BeginTime is null && b.EndTime is null;
        if (aAllDay || bAllDay)
        {
            return true;
        }

        // BeginTime inclusive, EndTime exclusive → A.Begin < B.End AND B.Begin < A.End
        return a.BeginTime!.Value < b.EndTime!.Value
               && b.BeginTime!.Value < a.EndTime!.Value;
    }

    private static bool DayOverlap(VariantSnapshot a, VariantSnapshot b)
    {
        if (a.DayMask is null || b.DayMask is null)
        {
            return true;
        }

        return (a.DayMask.Value & b.DayMask.Value) != 0;
    }

    private static bool AreaOverlap(VariantSnapshot a, VariantSnapshot b)
    {
        if (a.AppliesToAllAreas || b.AppliesToAllAreas)
        {
            return true;
        }

        // Cả 2 đều chỉ định area cụ thể → cần giao
        if (a.AreaIds.Count == 0 || b.AreaIds.Count == 0)
        {
            return false;
        }

        var set = new HashSet<int>(a.AreaIds);
        foreach (int id in b.AreaIds)
        {
            if (set.Contains(id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Variant draft đang được validate. AreaIds rỗng khi AppliesToAllAreas=true.</summary>
    public sealed record VariantSnapshot(
        int Id,
        string Code,
        TimeOnly? BeginTime,
        TimeOnly? EndTime,
        int? DayMask,
        bool AppliesToAllAreas,
        IReadOnlyCollection<int> AreaIds);
}
