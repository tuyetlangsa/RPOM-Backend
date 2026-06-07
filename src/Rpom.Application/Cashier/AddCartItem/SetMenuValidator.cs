using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.AddCartItem;

/// <summary>
///     Pure set-menu selection validator (no DB). Given the set menu's spec and the cashier's
///     selected details, enforces fixed-component presence, per-choice-category min/max counts,
///     per-modifier min/max quantity, and membership, then computes the extra ChoicePricePerUnit
///     (Σ modifier ExtraPrice × qty). Components carry no extra price — the set base price covers them.
/// </summary>
public static class SetMenuValidator
{
    private static readonly ValidationResult Invalid = new(false, 0m);

    public static ValidationResult Validate(Spec spec, IReadOnlyList<Selection> selections)
    {
        // --- Main components ---
        var componentByItem = spec.Components.ToDictionary(c => c.ItemId);
        var selectedComponents = selections
            .Where(s => s.ComponentType == ComponentType.MainComponent)
            .ToList();

        // Submitted components must be real components of the set menu.
        if (selectedComponents.Any(s => !componentByItem.ContainsKey(s.ItemId)))
        {
            return Invalid;
        }

        // Every fixed component must be present.
        var selectedComponentIds = selectedComponents.Select(s => s.ItemId).ToHashSet();
        if (spec.Components.Any(c => c.IsFixed && !selectedComponentIds.Contains(c.ItemId)))
        {
            return Invalid;
        }

        // --- Choice categories / modifiers ---
        decimal choicePrice = 0m;
        var modifierSelections = selections
            .Where(s => s.ComponentType == ComponentType.Modifier)
            .ToList();

        // Every modifier selection must name a choice category belonging to the set menu.
        var ccById = spec.ChoiceCategories.ToDictionary(c => c.ChoiceCategoryId);
        if (modifierSelections.Any(s => s.ChoiceCategoryId is null || !ccById.ContainsKey(s.ChoiceCategoryId.Value)))
        {
            return Invalid;
        }

        var selByCc = modifierSelections.GroupBy(s => s.ChoiceCategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (ChoiceCategorySpec cc in spec.ChoiceCategories)
        {
            List<Selection> sels = selByCc.GetValueOrDefault(cc.ChoiceCategoryId) ?? [];
            var modById = cc.Modifiers.ToDictionary(m => m.ItemId);

            // Count of distinct chosen modifiers must respect MinChoice..MaxChoice.
            int distinctCount = sels.Select(s => s.ItemId).Distinct().Count();
            if (distinctCount < cc.MinChoice)
            {
                return Invalid;
            }

            if (cc.MaxChoice is { } max && distinctCount > max)
            {
                return Invalid;
            }

            foreach (Selection sel in sels)
            {
                if (!modById.TryGetValue(sel.ItemId, out ModifierSpec? mod))
                {
                    return Invalid; // not an option of this CC
                }

                if (sel.Quantity < mod.MinPerModifier || sel.Quantity > mod.MaxPerModifier)
                {
                    return Invalid;
                }

                choicePrice += mod.ExtraPrice * sel.Quantity;
            }
        }

        return new ValidationResult(true, choicePrice);
    }

    public sealed record ComponentSpec(int ItemId, bool IsFixed);

    public sealed record ModifierSpec(int ItemId, int MinPerModifier, int MaxPerModifier, decimal ExtraPrice);

    public sealed record ChoiceCategorySpec(
        int ChoiceCategoryId,
        short MinChoice,
        short? MaxChoice,
        IReadOnlyList<ModifierSpec> Modifiers);

    public sealed record Spec(
        IReadOnlyList<ComponentSpec> Components,
        IReadOnlyList<ChoiceCategorySpec> ChoiceCategories);

    /// <summary>A submitted detail line: a main component or a chosen modifier.</summary>
    public sealed record Selection(int? ChoiceCategoryId, int ItemId, string ComponentType, decimal Quantity);

    public sealed record ValidationResult(bool IsValid, decimal ChoicePricePerUnit);
}
