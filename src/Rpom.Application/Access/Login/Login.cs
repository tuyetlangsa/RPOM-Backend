using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.Login;

public static class Login
{
    public sealed record Command(string Username, string Password) : ICommand<Response>;

    public sealed record Response(
        string AccessToken,
        DateTime ExpiresAt,
        int StaffAccountId,
        string Username,
        string FullName,
        string RoleCode);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Password).NotEmpty().MaximumLength(255);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IDateTimeProvider dateTimeProvider)
        : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var staff = await dbContext.StaffAccounts
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Username == request.Username, cancellationToken);

            if (staff is null)
                return Result.Failure<Response>(AccessErrors.InvalidCredentials);

            if (!staff.IsActive)
                return Result.Failure<Response>(AccessErrors.AccountInactive);

            if (staff.IsLocked)
                return Result.Failure<Response>(AccessErrors.AccountLocked);

            if (!passwordHasher.Verify(request.Password, staff.PasswordHash))
                return Result.Failure<Response>(AccessErrors.InvalidCredentials);

            var now = dateTimeProvider.UtcNow;
            staff.LastLoginAt = now;
            staff.UpdatedAt = now;

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(StaffAccount),
                EntityId = staff.Id,
                Action = "LOGIN",
                ActorStaffAccountId = staff.Id,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Login successful: {staff.Username}"
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            // NOTE: Personal shift attendance (StaffWorkSession) will record
            // login as a clock-in event in a future feature. For now login
            // only issues a JWT — cash drawer is opened separately at the
            // cashier app via POST /api/cash-drawers when needed.
            var token = jwtTokenService.IssueAccessToken(staff.Id, staff.Username);

            return Result.Success(new Response(
                AccessToken: token.Token,
                ExpiresAt: token.ExpiresAt,
                StaffAccountId: staff.Id,
                Username: staff.Username,
                FullName: staff.FullName,
                RoleCode: staff.Role.Code));
        }
    }
}
