using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Cashier.GetMenu;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.AddCartItem;

/// <summary>
///     Add an item (single or set menu) to the ticket's DRAFT cart in one call. For set menus,
///     the selected components/modifiers are validated against the spec (fixed components,
///     min/max counts and quantities) and ChoicePricePerUnit is computed server-side from DB
///     ExtraPrice. Price is resolved via the shared resolver; the line is recomputed by the cart
///     service. Requires the table operation lock.
///     <para>
///     Merge rule: a note-free add folds into an existing identical note-free draft line (quantity
///     bumped) instead of inserting a duplicate — singles match on item, set menus match on item +
///     the full component/modifier selection. Any add carrying a line note is always a new line.
///     </para>
/// </summary>
public static class AddCartItem
{
    public sealed record DetailInput(
        int? ChoiceCategoryId,
        int ItemId,
        string ComponentType,
        decimal Quantity,
        string? Notes);

    public sealed record Command(
        long TicketId,
        int ItemId,
        decimal Quantity,
        string? Notes,
        IReadOnlyList<DetailInput> Details) : ICommand<Response>;

    public sealed record Response(long CartItemId, long OrderId, decimal LineTotal);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.ItemId).GreaterThan(0);
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleForEach(x => x.Details).ChildRules(d =>
            {
                d.RuleFor(x => x.ItemId).GreaterThan(0);
                d.RuleFor(x => x.Quantity).GreaterThan(0);
                d.RuleFor(x => x.ComponentType)
                    .Must(t => t == ComponentType.MainComponent || t == ComponentType.Modifier);
            });
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        ITableOperationGuard guard,
        IMenuPriceResolver priceResolver,
        IRoundingConfig rc,
        ICartRecomputeService cartRecompute,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var ticket = await db.Tickets
                .Where(t => t.Id == request.TicketId)
                .Select(t => new
                {
                    t.Id,
                    t.TableId,
                    t.AreaId,
                    t.Status,
                    t.ServiceChargePercent,
                    t.ServiceChargeVatPercent
                })
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

            var item = await db.Items
                .Where(i => i.Id == request.ItemId && i.IsActive)
                .Select(i => new
                {
                    i.Id,
                    i.Code,
                    i.Name,
                    i.VatPercent,
                    i.BaseUomId,
                    UomCode = i.BaseUom.Code,
                    UomName = i.BaseUom.Name,
                    IsSetMenu = i.SetMenu != null
                })
                .FirstOrDefaultAsync(ct);
            if (item is null)
            {
                return Result.Failure<Response>(OrderErrors.ItemNotFound);
            }

            DateTime now = clock.UtcNow;
            MenuPriceResolution resolution =
                await priceResolver.ResolveAsync(ticket.AreaId, now, new[] { item.Id }, ct);
            if (!resolution.Prices.TryGetValue(item.Id, out ResolvedPrice resolved))
            {
                return Result.Failure<Response>(OrderErrors.ItemNotPriced);
            }

            (decimal basePrice, _) =
                MenuPricing.ComputePrices(resolved.Price, resolved.IsVatIncluded, item.VatPercent, rc);

            // Validate selection + compute extra modifier price.
            decimal choicePrice = 0m;
            IReadOnlyList<DetailRow> detailRows = [];
            if (item.IsSetMenu)
            {
                BuildResult built = await BuildAndValidateSetMenuAsync(item.Id, request.Details, ct);
                if (built.Error is { } err)
                {
                    return Result.Failure<Response>(err);
                }

                choicePrice = built.ChoicePricePerUnit;
                detailRows = built.Rows;
            }
            else if (request.Details.Count > 0)
            {
                return Result.Failure<Response>(OrderErrors.DetailsNotAllowed);
            }

            // Get-or-create the ticket's DRAFT order.
            Order? order = await db.Orders
                .FirstOrDefaultAsync(o => o.TicketId == ticket.Id && o.Status == OrderStatus.Draft, ct);
            bool orderExisted = order is not null;
            if (order is null)
            {
                short maxNo = await db.Orders.Where(o => o.TicketId == ticket.Id)
                    .Select(o => (short?)o.OrderNumber).MaxAsync(ct) ?? 0;
                order = new Order
                {
                    TicketId = ticket.Id,
                    OrderNumber = (short)(maxNo + 1),
                    Status = OrderStatus.Draft,
                    CreatedByStaffId = currentStaff.StaffAccountId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Orders.Add(order);
                await db.SaveChangesAsync(ct); // need order.Id for the cart item
            }

            string? notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            // Merge into an existing identical draft line instead of inserting a duplicate.
            // A line carrying a note is ALWAYS kept separate — the kitchen must see each distinct
            // instruction on its own line. Only note-free lines are candidates for quantity merge.
            // (A brand-new order can have no prior line, so skip the lookup entirely.)
            if (orderExisted && notes is null)
            {
                CartItem? mergeTarget =
                    await FindMergeTargetAsync(order.Id, item.Id, item.IsSetMenu, detailRows, ct);
                if (mergeTarget is not null)
                {
                    mergeTarget.Quantity += request.Quantity;
                    mergeTarget.UpdatedAt = now;

                    await cartRecompute.RecomputeAsync(order.Id, ct);
                    await db.SaveChangesAsync(ct);
                    await versionService.BumpAsync(
                        VersionScopes.FloorPlan, $"Cart.Add(ticketId={ticket.Id})", ct);

                    return Result.Success(
                        new Response(mergeTarget.Id, order.Id, mergeTarget.LineTotal));
                }
            }

            var cartItem = new CartItem
            {
                OrderId = order.Id,
                ItemId = item.Id,
                ItemCode = item.Code,
                ItemName = item.Name,
                UomId = item.BaseUomId,
                UomCode = item.UomCode,
                UomName = item.UomName,
                Quantity = request.Quantity,
                UnitPrice = basePrice,
                ChoicePricePerUnit = choicePrice,
                VatPercent = item.VatPercent,
                ServiceChargePercent = ticket.ServiceChargePercent,
                ServiceChargeVatPercent = ticket.ServiceChargeVatPercent,
                Notes = notes,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.CartItems.Add(cartItem);
            await db.SaveChangesAsync(ct); // need cartItem.Id for detail rows

            foreach (DetailRow d in detailRows)
            {
                db.CartItemDetails.Add(new CartItemDetail
                {
                    CartItemId = cartItem.Id,
                    ChoiceCategoryId = d.ChoiceCategoryId,
                    ItemId = d.ItemId,
                    ItemName = d.ItemName,
                    ComponentType = d.ComponentType,
                    Quantity = d.Quantity,
                    ExtraPrice = d.ExtraPrice,
                    Notes = d.Notes,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await cartRecompute.RecomputeAsync(order.Id, ct);
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Cart.Add(ticketId={ticket.Id})", ct);

            return Result.Success(new Response(cartItem.Id, order.Id, cartItem.LineTotal));
        }

        /// <summary>
        ///     Finds a note-free draft line in the same order that the incoming add can fold into.
        ///     Single items match on ItemId alone. Set menus must additionally match the FULL
        ///     component/modifier selection — every detail (ChoiceCategory, Item, type, quantity,
        ///     per-component note) compared as an order-independent multiset. Returns the tracked
        ///     entity so the caller can bump its quantity; null when no identical line exists.
        /// </summary>
        private async Task<CartItem?> FindMergeTargetAsync(
            long orderId, int itemId, bool isSetMenu, IReadOnlyList<DetailRow> incoming, CancellationToken ct)
        {
            List<CartItem> candidates = await db.CartItems
                .Where(c => c.OrderId == orderId && c.ItemId == itemId && c.Notes == null)
                .ToListAsync(ct);
            if (candidates.Count == 0)
            {
                return null;
            }

            if (!isSetMenu)
            {
                // Single item has no details to compare — any note-free line of the same item folds.
                return candidates[0];
            }

            List<long> candidateIds = candidates.Select(c => c.Id).ToList();
            Dictionary<long, List<string>> keysByCart = (await db.CartItemDetails
                    .Where(d => candidateIds.Contains(d.CartItemId))
                    .Select(d => new
                    { d.CartItemId, d.ChoiceCategoryId, d.ItemId, d.ComponentType, d.Quantity, d.Notes })
                    .ToListAsync(ct))
                .GroupBy(d => d.CartItemId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(d => DetailKey(d.ChoiceCategoryId, d.ItemId, d.ComponentType, d.Quantity, d.Notes))
                        .OrderBy(k => k, StringComparer.Ordinal).ToList());

            List<string> incomingKeys = incoming
                .Select(d => DetailKey(d.ChoiceCategoryId, d.ItemId, d.ComponentType, d.Quantity, d.Notes))
                .OrderBy(k => k, StringComparer.Ordinal).ToList();

            return candidates.FirstOrDefault(c =>
            {
                List<string> keys = keysByCart.GetValueOrDefault(c.Id) ?? [];
                return keys.Count == incomingKeys.Count && keys.SequenceEqual(incomingKeys, StringComparer.Ordinal);
            });
        }

        /// <summary>Canonical, order-independent key for one component/modifier selection.</summary>
        private static string DetailKey(
            int? choiceCategoryId, int itemId, string componentType, decimal quantity, string? notes)
        {
            string cc = choiceCategoryId?.ToString(CultureInfo.InvariantCulture) ?? "_";
            string n = string.IsNullOrWhiteSpace(notes) ? "_" : notes.Trim();
            // Scale-independent quantity: stored decimals come back with column scale (1.00000),
            // incoming literals do not (1) — strip trailing zeros so both keys match.
            string q = quantity.ToString("0.############################", CultureInfo.InvariantCulture);
            return $"{cc}|{itemId}|{componentType}|{q}|{n}";
        }

        private async Task<BuildResult> BuildAndValidateSetMenuAsync(
            int setItemId, IReadOnlyList<DetailInput> details, CancellationToken ct)
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

            var spec = new SetMenuValidator.Spec(componentSpecs, ccSpecs);
            var selections = details
                .Select(d => new SetMenuValidator.Selection(d.ChoiceCategoryId, d.ItemId, d.ComponentType, d.Quantity))
                .ToList();

            SetMenuValidator.ValidationResult result = SetMenuValidator.Validate(spec, selections);
            if (!result.IsValid)
            {
                return new BuildResult(OrderErrors.InvalidSetMenuSelection, 0m, []);
            }

            // Snapshot names + extra prices for persistence.
            var componentNames = await db.Items
                .Where(i => componentSpecs.Select(c => c.ItemId).Contains(i.Id))
                .Select(i => new { i.Id, i.Name })
                .ToListAsync(ct);
            var nameByComponent = componentNames.ToDictionary(x => x.Id, x => x.Name);
            var modByKey = modDefs.ToDictionary(m => (m.ChoiceCategoryId, m.ItemId));

            var rows = details.Select(d =>
            {
                if (d.ComponentType == ComponentType.Modifier)
                {
                    var m = modByKey[(d.ChoiceCategoryId!.Value, d.ItemId)];
                    return new DetailRow(d.ChoiceCategoryId, d.ItemId, m.ItemName, d.ComponentType,
                        d.Quantity, m.ExtraPrice, d.Notes);
                }

                return new DetailRow(null, d.ItemId, nameByComponent.GetValueOrDefault(d.ItemId, ""),
                    d.ComponentType, d.Quantity, 0m, d.Notes);
            }).ToList();

            return new BuildResult(null, result.ChoicePricePerUnit, rows);
        }

        private sealed record DetailRow(
            int? ChoiceCategoryId,
            int ItemId,
            string ItemName,
            string ComponentType,
            decimal Quantity,
            decimal ExtraPrice,
            string? Notes);

        private sealed record BuildResult(Error? Error, decimal ChoicePricePerUnit, IReadOnlyList<DetailRow> Rows);
    }
}
