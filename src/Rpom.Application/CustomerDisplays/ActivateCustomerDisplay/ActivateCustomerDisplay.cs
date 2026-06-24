using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.CustomerDisplays.ActivateCustomerDisplay;
public static class ActivateCustomerDisplay
{
    public sealed record Command(string DeviceToken, string ClientId) : ICommand<Response>;

    public sealed record Response(int DisplayId, string Name, int PosTerminalId);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DeviceToken).NotEmpty().MaximumLength(64);
            RuleFor(x => x.ClientId).NotEmpty().MaximumLength(64);
        }
    }

    internal sealed class Handler(IDbContext db, IDateTimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            CustomerDisplay? display = await db.CustomerDisplays
                .FirstOrDefaultAsync(d => d.DeviceToken == request.DeviceToken && d.IsActive, ct);
            if (display is null) return Result.Failure<Response>(CustomerDisplayErrors.InvalidToken);

            string clientId = request.ClientId.Trim();
            if (display.ActivatedClientId is { } existing)
            {
                if (existing != clientId)
                    return Result.Failure<Response>(CustomerDisplayErrors.AlreadyActivated);
            }
            else
            {
                display.ActivatedClientId = clientId;
                display.ActivatedAt = clock.UtcNow;
                display.UpdatedAt = clock.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return Result.Success(new Response(display.Id, display.Name, display.PosTerminalId));
        }
    }
}
