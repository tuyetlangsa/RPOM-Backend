using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Configuration;

namespace Rpom.Application.Configuration.UpdateRoundingConfig;

public static class UpdateRoundingConfig
{
    public sealed record Command(string KeyCode, short Digits) : ICommand<Response>;

    public sealed record Response(string KeyCode, short Digits, DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.KeyCode).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Digits).InclusiveBetween((short)0, (short)4);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        IDateTimeProvider clock,
        IVersionService versionService,
        IRoundingCacheInvalidator cacheInvalidator) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var row = await dbContext.RoundingConfigs
                .FirstOrDefaultAsync(x => x.KeyCode == request.KeyCode, ct);
            if (row is null)
                return Result.Failure<Response>(RoundingConfigErrors.NotFound);

            row.Digits = request.Digits;
            row.UpdatedAt = clock.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            cacheInvalidator.Invalidate();
            await versionService.BumpAsync(
                VersionScopes.Config, $"RoundingConfig.Update({row.KeyCode})", ct);

            return Result.Success(new Response(row.KeyCode, row.Digits, row.UpdatedAt));
        }
    }
}
