using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Cashier.AddCartItem;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.UpdateCartItem;

/// <summary>
///     Update a cart line: change quantity, notes, and for set menus reconfigure modifiers.
///     FE sends the complete desired modifier set; BE diffs against existing details:
///     matched → update qty, qty=0 → delete, new → insert. The final state is validated
///     against the set-menu spec. Main-item qty=0 removes the entire cart line.
///     Requires the table lock; recomputes the cart.
/// </summary>
public static class UpdateCartItem
{
    public sealed record DetailInput(
        int? ChoiceCategoryId,
        int ItemId,
        string ComponentType,
        decimal Quantity,
        string? Notes);

    public sealed record Command(
        long TicketId,
        long CartItemId,
        decimal Quantity,
        string? Notes,
        IReadOnlyList<DetailInput>? Details) : ICommand<Response>;

    public sealed record Response(long CartItemId, decimal LineTotal);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        ITableOperationGuard guard,
        ICartRecomputeService cartRecompute,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var ticket = await db.Tickets
                .Where(t => t.Id == request.TicketId)
                .Select(t => new { t.Id, t.TableId, t.Status })
                .FirstOrDefaultAsync(ct);
            if (ticket is null)
            {
                return Result.Failure<Response>(TicketErrors.NotFound);
            }

            if (ticket.Status != TicketStatus.Open)
            {
                return Result.Failure<Response>(TicketErrors.NotOpen);
            }

            Result held = await guard.EnsureHeldAsync(ticket.TableId, currentStaff.StaffAccountId, ct);
            if (held.IsFailure)
            {
                return Result.Failure<Response>(held.Error);
            }

            // Cart item must belong to this ticket's DRAFT order.
            CartItem? cartItem = await db.CartItems
                .Where(c => c.Id == request.CartItemId
                            && db.Orders.Any(o => o.Id == c.OrderId
                                                  && o.TicketId == ticket.Id && o.Status == OrderStatus.Draft))
                .FirstOrDefaultAsync(ct);
            if (cartItem is null)
            {
                return Result.Failure<Response>(OrderErrors.CartItemNotFound);
            }

            DateTime now = clock.UtcNow;

            // Main qty → 0 = remove the cart line.
            if (request.Quantity <= 0)
            {
                long orderId = cartItem.OrderId;
                db.CartItems.Remove(cartItem);
                await db.SaveChangesAsync(ct);
                await cartRecompute.RecomputeAsync(orderId, ct);
                await db.SaveChangesAsync(ct);
                await versionService.BumpAsync(VersionScopes.FloorPlan,
                    $"Cart.RemoveViaUpdate(ticketId={ticket.Id})", ct);
                return Result.Success(new Response(0, 0));
            }

            // ---- Modifier reconfiguration (set menu only) ----
            if (request.Details is not null)
            {
                var item = await db.Items
                    .Where(i => i.Id == cartItem.ItemId)
                    .Select(i => new { i.Id, IsSetMenu = i.SetMenu != null })
                    .FirstOrDefaultAsync(ct);
                if (item is null)
                {
                    return Result.Failure<Response>(OrderErrors.ItemNotFound);
                }

                if (!item.IsSetMenu)
                {
                    return Result.Failure<Response>(OrderErrors.DetailsNotAllowed);
                }

                DetailDiffResult result = await ApplyDetailDiffAsync(cartItem, request.Details, now, ct);
                if (result.Error is { } err)
                {
                    return Result.Failure<Response>(err);
                }

                cartItem.ChoicePricePerUnit = result.ChoicePricePerUnit;
            }

            cartItem.Quantity = request.Quantity;
            cartItem.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            cartItem.UpdatedAt = now;

            await cartRecompute.RecomputeAsync(cartItem.OrderId, ct);
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan,
                $"Cart.Update(ticketId={ticket.Id})", ct);

            return Result.Success(new Response(cartItem.Id, cartItem.LineTotal));
        }

        // ---- Detail diff engine ----

        /// <summary>Match key for diffing CartItemDetails.</summary>
        private static (int ccId, int itemId, string type) Key(CartItemDetail d) =>
            (d.ChoiceCategoryId ?? 0, d.ItemId, d.ComponentType);

        private static (int ccId, int itemId, string type) Key(DetailInput d) =>
            (d.ChoiceCategoryId ?? 0, d.ItemId, d.ComponentType);

        private async Task<DetailDiffResult> ApplyDetailDiffAsync(
            CartItem cartItem,
            IReadOnlyList<DetailInput> incoming,
            DateTime now,
            CancellationToken ct)
        {
            // ---- Load existing details + set-menu spec ----
            List<CartItemDetail> existing = await db.CartItemDetails
                .Where(d => d.CartItemId == cartItem.Id)
                .ToListAsync(ct);

            SetMenuValidator.Spec? spec = await BuildSetMenuSpecAsync(cartItem.ItemId, ct);
            if (spec is null)
            {
                return new DetailDiffResult(OrderErrors.ItemNotFound, 0m);
            }

            // ---- Diff ----
            var existingByKey = existing.ToDictionary(Key);
            var incomingByKey = incoming
                .Where(d => d.Quantity > 0)
                .ToDictionary(Key);

            var toDelete = new List<CartItemDetail>();
            var toInsert = new List<(DetailInput Detail, string ItemName, decimal ExtraPrice)>();
            var toUpdate = new List<(CartItemDetail Entity, decimal NewQty, string? NewNotes)>();

            foreach (CartItemDetail e in existing)
            {
                (int ccId, int itemId, string type) k = Key(e);
                if (incomingByKey.TryGetValue(k, out DetailInput? inc))
                {
                    toUpdate.Add((e, inc.Quantity, inc.Notes));
                }
                else
                {
                    toDelete.Add(e);
                }
            }

            foreach (((int ccId, int itemId, string type) k, DetailInput inc) in incomingByKey)
            {
                if (!existingByKey.ContainsKey(k))
                {
                    (string itemName, decimal extraPrice) = await ResolveDetailMetaAsync(inc, ct);
                    toInsert.Add((inc, itemName, extraPrice));
                }
            }

            // ---- Build final selection for validation ----
            var updatedDetails = existing
                .Except(toDelete)
                .Select(e =>
                {
                    (CartItemDetail Entity, decimal NewQty, string? NewNotes) match =
                        toUpdate.FirstOrDefault(u => u.Entity.Id == e.Id);
                    decimal qty = match.Entity is not null ? match.NewQty : e.Quantity;
                    return new SetMenuValidator.Selection(e.ChoiceCategoryId, e.ItemId, e.ComponentType, qty);
                })
                .Concat(toInsert.Select(i =>
                    new SetMenuValidator.Selection(i.Detail.ChoiceCategoryId, i.Detail.ItemId,
                        i.Detail.ComponentType, i.Detail.Quantity)))
                .ToList();

            SetMenuValidator.ValidationResult validation = SetMenuValidator.Validate(spec, updatedDetails);
            if (!validation.IsValid)
            {
                return new DetailDiffResult(OrderErrors.InvalidSetMenuSelection, 0m);
            }

            // ---- Apply changes ----
            foreach ((CartItemDetail entity, decimal newQty, string? newNotes) in toUpdate)
            {
                entity.Quantity = newQty;
                entity.Notes = string.IsNullOrWhiteSpace(newNotes) ? null : newNotes.Trim();
                entity.UpdatedAt = now;
            }

            db.CartItemDetails.RemoveRange(toDelete);

            foreach ((DetailInput detail, string itemName, decimal extraPrice) in toInsert)
            {
                db.CartItemDetails.Add(new CartItemDetail
                {
                    CartItemId = cartItem.Id,
                    ChoiceCategoryId = detail.ChoiceCategoryId,
                    ItemId = detail.ItemId,
                    ItemName = itemName,
                    ComponentType = detail.ComponentType,
                    Quantity = detail.Quantity,
                    ExtraPrice = extraPrice,
                    Notes = string.IsNullOrWhiteSpace(detail.Notes) ? null : detail.Notes.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            return new DetailDiffResult(null, validation.ChoicePricePerUnit);
        }

        // ---- Set-menu spec loader (shared shape with AddCartItem) ----

        private async Task<SetMenuValidator.Spec?> BuildSetMenuSpecAsync(int setItemId, CancellationToken ct)
        {
            var detailDefs = await db.SetMenuDetails
                .Where(d => d.SetMenuItemId == setItemId)
                .Select(d => new { d.DetailType, d.ComponentItemId, d.ChoiceCategoryId, d.IsFixed })
                .ToListAsync(ct);

            var componentSpecs = detailDefs
                .Where(d => d.DetailType == SetMenuDetailType.Component && d.ComponentItemId != null)
                .Select(d => new SetMenuValidator.ComponentSpec(d.ComponentItemId!.Value, d.IsFixed ?? false))
                .ToList();

            var ccIds = detailDefs
                .Where(d => d.DetailType == SetMenuDetailType.ChoiceCategory && d.ChoiceCategoryId != null)
                .Select(d => d.ChoiceCategoryId!.Value).Distinct().ToList();

            if (ccIds.Count == 0)
            {
                return new SetMenuValidator.Spec(componentSpecs, []);
            }

            var ccDefs = await db.ChoiceCategories
                .Where(cc => ccIds.Contains(cc.Id) && cc.IsActive)
                .Select(cc => new { cc.Id, cc.MinChoice, cc.MaxChoice })
                .ToListAsync(ct);

            var modDefs = await db.Modifiers
                .Where(m => ccIds.Contains(m.ChoiceCategoryId) && m.IsActive)
                .Select(m => new
                {
                    m.ChoiceCategoryId,
                    m.ItemId,
                    m.MinPerModifier,
                    m.MaxPerModifier,
                    m.ExtraPrice,
                    ItemName = m.Item.Name
                })
                .ToListAsync(ct);
            var modByCc = modDefs.GroupBy(m => m.ChoiceCategoryId).ToDictionary(g => g.Key, g => g.ToList());

            var ccSpecs = ccDefs.Select(cc => new SetMenuValidator.ChoiceCategorySpec(
                cc.Id, cc.MinChoice, cc.MaxChoice,
                (modByCc.GetValueOrDefault(cc.Id) ?? [])
                .Select(m =>
                    new SetMenuValidator.ModifierSpec(m.ItemId, m.MinPerModifier, m.MaxPerModifier, m.ExtraPrice))
                .ToList())).ToList();

            return new SetMenuValidator.Spec(componentSpecs, ccSpecs);
        }

        /// <summary>Resolve item name + extra price for a new detail line about to be inserted.</summary>
        private async Task<(string ItemName, decimal ExtraPrice)> ResolveDetailMetaAsync(
            DetailInput detail, CancellationToken ct)
        {
            if (detail.ComponentType == ComponentType.Modifier && detail.ChoiceCategoryId is { } ccId)
            {
                var mod = await db.Modifiers
                    .Where(m => m.ChoiceCategoryId == ccId && m.ItemId == detail.ItemId && m.IsActive)
                    .Select(m => new { m.ExtraPrice, ItemName = m.Item.Name })
                    .FirstOrDefaultAsync(ct);
                return mod is not null ? (mod.ItemName, mod.ExtraPrice) : ("", 0m);
            }

            string? name = await db.Items
                .Where(i => i.Id == detail.ItemId)
                .Select(i => i.Name)
                .FirstOrDefaultAsync(ct);
            return (name ?? "", 0m);
        }

        private sealed record DetailDiffResult(Error? Error, decimal ChoicePricePerUnit);
    }
}
