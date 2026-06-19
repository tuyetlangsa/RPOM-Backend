using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Operations.GetNotifications;

namespace Rpom.Api.Endpoints.System.Notifications;

internal sealed class GetNotificationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/notifications",
                async ([FromQuery] int counterId, [FromQuery] long? sinceId,
                    [FromQuery] int? limit, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new GetNotifications.Query(counterId, sinceId, limit ?? 50), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.NotificationView)
            .Produces<ApiResult<GetNotifications.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Notifications")
            .WithName("GetNotifications")
            .WithSummary("Polls report out-of-stock situations at each counter. Cashiers and order staff use the counterId and sinceId cursors.");
    }
}
