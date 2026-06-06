using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.PriceEntries.UpsertPriceEntries;

/// <summary>
/// Full-snapshot bulk upsert. FE gửi nguyên list entries hiện tại của variant;
/// BE diff với DB: thêm mới, cập nhật giá, xoá row không có trong payload.
/// </summary>
public static class UpsertPriceEntries
{
    public sealed record EntryInput(int ItemId, decimal Price, bool IsVatIncluded);

    public sealed record Command(
        int PriceVariantId,
        IReadOnlyList<EntryInput> Entries) : ICommand<Response>;

    public sealed record Response(
        int PriceVariantId,
        int Inserted,
        int Updated,
        int Deleted,
        int TotalEntries);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PriceVariantId).GreaterThan(0);
            RuleFor(x => x.Entries).NotNull();
            RuleForEach(x => x.Entries).ChildRules(e =>
            {
                e.RuleFor(x => x.ItemId).GreaterThan(0);
                e.RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
            });
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var variantExists = await dbContext.PriceVariants
                .AnyAsync(v => v.Id == request.PriceVariantId, ct);
            if (!variantExists) return Result.Failure<Response>(PriceEntryErrors.VariantNotFound);

            // Duplicate ItemId trong payload
            var itemIds = request.Entries.Select(e => e.ItemId).ToList();
            if (itemIds.Count != itemIds.Distinct().Count())
                return Result.Failure<Response>(PriceEntryErrors.DuplicateItem);

            // Validate Item tồn tại
            if (itemIds.Count > 0)
            {
                var distinctIds = itemIds.Distinct().ToList();
                var foundCount = await dbContext.Items.CountAsync(i => distinctIds.Contains(i.Id), ct);
                if (foundCount != distinctIds.Count)
                    return Result.Failure<Response>(PriceEntryErrors.ItemNotFound);
            }

            var existing = await dbContext.PriceEntries
                .Where(e => e.PriceVariantId == request.PriceVariantId)
                .ToListAsync(ct);

            var now = clock.UtcNow;
            var staffId = currentStaff.StaffAccountId;
            var payloadByItem = request.Entries.ToDictionary(e => e.ItemId);
            var existingByItem = existing.ToDictionary(e => e.ItemId);

            var inserted = 0;
            var updated = 0;
            var deleted = 0;

            // Update + Insert
            foreach (var input in request.Entries)
            {
                if (existingByItem.TryGetValue(input.ItemId, out var row))
                {
                    if (row.Price != input.Price || row.IsVatIncluded != input.IsVatIncluded)
                    {
                        row.Price = input.Price;
                        row.IsVatIncluded = input.IsVatIncluded;
                        row.UpdatedAt = now;
                        updated++;
                    }
                }
                else
                {
                    dbContext.PriceEntries.Add(new PriceEntry
                    {
                        PriceVariantId = request.PriceVariantId,
                        ItemId = input.ItemId,
                        Price = input.Price,
                        IsVatIncluded = input.IsVatIncluded,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });
                    inserted++;
                }
            }

            // Delete những row tồn tại nhưng không có trong payload
            foreach (var row in existing)
            {
                if (!payloadByItem.ContainsKey(row.ItemId))
                {
                    dbContext.PriceEntries.Remove(row);
                    deleted++;
                }
            }

            if (inserted > 0 || updated > 0 || deleted > 0)
            {
                var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
                dbContext.AuditLogs.Add(new AuditLog
                {
                    EntityType = nameof(PriceVariant),
                    EntityId = request.PriceVariantId,
                    Action = "UPSERT_ENTRIES",
                    ActorStaffAccountId = staffId,
                    ActorFullName = staff.FullName,
                    Timestamp = now,
                    Summary = $"PriceEntries bulk upsert: +{inserted} ~{updated} -{deleted} on variant {request.PriceVariantId}",
                });
                await dbContext.SaveChangesAsync(ct);
                await versionService.BumpAsync(VersionScopes.Pricing,
                    $"PriceEntries.Upsert(variantId={request.PriceVariantId})", ct);
            }

            return Result.Success(new Response(
                request.PriceVariantId, inserted, updated, deleted, request.Entries.Count));
        }
    }
}
