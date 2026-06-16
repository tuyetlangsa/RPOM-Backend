using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Inventory;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Inventory;

namespace Rpom.Application.Inventory.CreateStockMovement;

public static class CreateStockMovement
{
    public sealed record Command(
        int ItemId,
        string MovementType,
        decimal Quantity,
        string? Reason) : ICommand<Response>;

    public sealed record Response(
        long Id,
        int ItemId,
        string MovementType,
        decimal QtyInBase,
        decimal BalanceAfter,
        string? Reason,
        DateTime CreatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ItemId).GreaterThan(0);
            RuleFor(x => x.MovementType)
                .NotEmpty()
                .Must(m => m is StockMovementType.StockIn
                                or StockMovementType.AdjustIn
                                or StockMovementType.AdjustOut)
                .WithMessage("MovementType phai la STOCK_IN, ADJUST_IN, hoac ADJUST_OUT.");
            RuleFor(x => x.Quantity).GreaterThan(0m);
            RuleFor(x => x.Reason).MaximumLength(500);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IStockMovementService stockMovementService,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            // Validate item exists + IsStockable
            var item = await dbContext.Items
                .Where(x => x.Id == request.ItemId)
                .Select(x => new { x.IsStockable, x.Code, x.Name })
                .FirstOrDefaultAsync(ct);
            if (item is null) return Result.Failure<Response>(StockMovementErrors.ItemNotFound);
            if (!item.IsStockable) return Result.Failure<Response>(StockMovementErrors.ItemNotStockable);

            // Convert positive input qty to signed qty
            decimal signedQty = request.MovementType switch
            {
                StockMovementType.StockIn or StockMovementType.AdjustIn => request.Quantity,
                StockMovementType.AdjustOut => -request.Quantity,
                _ => throw new InvalidOperationException($"Unexpected movement type: {request.MovementType}")
            };

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            // Delegate to service — creates StockMovement + ItemStock in one flush
            var movement = await stockMovementService.CreateManualAsync(
                request.ItemId, request.MovementType, signedQty, request.Reason, staffId, now, ct);

            // Audit log
            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(StockMovement),
                EntityId = movement.Id,
                Action = request.MovementType,
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"{request.MovementType}: {item.Name} ({item.Code}), SL={request.Quantity}" +
                          (string.IsNullOrEmpty(request.Reason) ? "" : $" — {request.Reason}")
            });
            await dbContext.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.Menu,
                $"StockMovement.{request.MovementType}(id={movement.Id},itemId={request.ItemId})", ct);

            return Result.Success(new Response(
                movement.Id, movement.ItemId, request.MovementType,
                movement.QtyInBase, movement.BalanceAfter,
                request.Reason, movement.CreatedAt));
        }
    }
}
