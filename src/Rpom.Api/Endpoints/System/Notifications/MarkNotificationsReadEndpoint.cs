using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Operations.MarkNotificationsRead;

namespace Rpom.Api.Endpoints.System.Notifications;

internal sealed class MarkNotificationsReadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/notifications/read",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new MarkNotificationsRead.Command(request.CounterId, request.UpToId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.NotificationView)
            .Produces<ApiResult<MarkNotificationsRead.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Notifications")
            .WithName("MarkNotificationsRead")
            .WithSummary("Mark the counter notification as seen (push read-cursor) → unread badge resets to 0.");
    }

    internal sealed record Request(int CounterId, long? UpToId);
}
