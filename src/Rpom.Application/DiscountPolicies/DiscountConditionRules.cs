using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.DiscountPolicies;

/// <summary>
/// Shared field-usage rules for DiscountPolicyCondition, keyed by the parent
/// policy's DiscountType. Operates on raw nullable fields so each use case can
/// keep its own ConditionInput DTO.
/// </summary>
internal static class DiscountConditionRules
{
    /// <summary>
    /// Verify every referenced ItemId/AreaId exists. Returns the matching error,
    /// or null when all references resolve.
    /// </summary>
    public static async Task<Error?> ValidateReferencesAsync(
        IDbContext db, IEnumerable<(int? ItemId, int? AreaId)> refs, CancellationToken ct)
    {
        var refList = refs.ToList();
        var itemIds = refList.Where(r => r.ItemId.HasValue).Select(r => r.ItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            var found = await db.Items.CountAsync(i => itemIds.Contains(i.Id), ct);
            if (found != itemIds.Count) return DiscountPolicyErrors.ItemNotFound;
        }
        var areaIds = refList.Where(r => r.AreaId.HasValue).Select(r => r.AreaId!.Value).Distinct().ToList();
        if (areaIds.Count > 0)
        {
            var found = await db.Areas.CountAsync(a => areaIds.Contains(a.Id), ct);
            if (found != areaIds.Count) return DiscountPolicyErrors.AreaNotFound;
        }
        return null;
    }

    /// <summary>
    /// TICKET_THRESHOLD → ThresholdAmount &gt; 0, no ItemId/QuantityThreshold.
    /// QUANTITY_ITEM → ItemId set + QuantityThreshold &gt; 0, no ThresholdAmount.
    /// </summary>
    public static bool DiscriminatorValid(
        string discountType, decimal? thresholdAmount, int? itemId, decimal? quantityThreshold)
        => discountType switch
        {
            DiscountType.TicketThreshold =>
                thresholdAmount is > 0 && itemId is null && quantityThreshold is null,
            DiscountType.QuantityItem =>
                itemId is not null && quantityThreshold is > 0 && thresholdAmount is null,
            _ => false,
        };

    /// <summary>
    /// True when two conditions share the same trigger key (redundant/ambiguous).
    /// Trigger excludes ApplyType/DiscountValue/DisplayOrder, so tiered rules (different
    /// threshold/quantity) and different area scopes are still allowed:
    /// TICKET_THRESHOLD key = (ThresholdAmount, AreaId);
    /// QUANTITY_ITEM key = (ItemId, QuantityThreshold, AreaId).
    /// </summary>
    public static bool HasDuplicateTriggers(
        string discountType,
        IEnumerable<(decimal? ThresholdAmount, int? ItemId, decimal? QuantityThreshold, int? AreaId)> conditions)
    {
        var keys = conditions
            .Select(c => discountType == DiscountType.TicketThreshold
                ? $"T|{c.ThresholdAmount}|{c.AreaId}"
                : $"Q|{c.ItemId}|{c.QuantityThreshold}|{c.AreaId}")
            .ToList();
        return keys.Count != keys.Distinct().Count();
    }
}
