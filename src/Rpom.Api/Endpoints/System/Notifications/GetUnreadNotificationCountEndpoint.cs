using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Operations.GetUnreadNotificationCount;

namespace Rpom.Api.Endpoints.System.Notifications;

internal sealed class GetUnreadNotificationCountEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/notifications/unread-count",
                async ([FromQuery] int counterId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetUnreadNotificationCount.Query(counterId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.NotificationView)
            .Produces<ApiResult<GetUnreadNotificationCount.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Notifications")
            .WithName("GetUnreadNotificationCount")
            .WithSummary("The number of unread messages for the current staff member at the counter (bell badge icon).");
    }
}
