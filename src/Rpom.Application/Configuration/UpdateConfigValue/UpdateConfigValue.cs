using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Configuration;

namespace Rpom.Application.Configuration.UpdateConfigValue;

public static class UpdateConfigValue
{
    public sealed record Command(string Code, string? Value) : ICommand<Response>;

    public sealed record Response(string Code, string? Value, DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            ConfigValue? row = await dbContext.ConfigValues
                .FirstOrDefaultAsync(x => x.Code == request.Code, ct);
            if (row is null)
            {
                return Result.Failure<Response>(ConfigErrors.NotFound);
            }

            if (!ConfigValueType.IsValidValue(row.ValueType, request.Value))
            {
                return Result.Failure<Response>(ConfigErrors.InvalidValueForType);
            }

            string? oldValue = row.Value;
            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            row.Value = request.Value;
            row.UpdatedAt = now;
            row.UpdatedByStaffAccountId = staffId;

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ConfigValue),
                EntityId = 0, // ConfigValue PK is string Code, not int — store 0 + Summary
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Config '{request.Code}': '{oldValue}' → '{request.Value}'"
            });

            await dbContext.SaveChangesAsync(ct);

            return Result.Success(new Response(row.Code, row.Value, row.UpdatedAt));
        }
    }
}
