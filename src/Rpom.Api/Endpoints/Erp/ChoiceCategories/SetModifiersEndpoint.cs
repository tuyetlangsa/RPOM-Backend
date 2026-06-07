using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.ChoiceCategories.SetModifiers;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.ChoiceCategories;

internal sealed class SetModifiersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/choice-categories/{id:int}/modifiers",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var modifiers = (request.Modifiers ?? Array.Empty<ModifierRequest>())
                        .Select(m => new SetModifiers.ModifierInput(
                            m.ItemId, m.ExtraPrice, m.MinPerModifier, m.MaxPerModifier, m.DisplayOrder, m.IsActive))
                        .ToList();
                    Result<SetModifiers.Response> result =
                        await sender.Send(new SetModifiers.Command(id, modifiers), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("ChoiceCategories")
            .WithName("SetModifiers")
            .Produces<ApiResult<SetModifiers.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Replace all modifiers of a choice category.")
            .WithDescription(
                "Request: route id (int); JSON body { modifiers:[{ itemId:int, extraPrice:decimal, minPerModifier:int, maxPerModifier:int, displayOrder:short, isActive:bool }] }. Response: 200 OK — JSON SetModifiers.Response { choiceCategoryId, inserted, updated, deleted, total }.");
    }

    internal sealed record Request(IReadOnlyList<ModifierRequest>? Modifiers);

    internal sealed record ModifierRequest(
        int ItemId,
        decimal ExtraPrice,
        int MinPerModifier,
        int MaxPerModifier,
        short DisplayOrder,
        bool IsActive);
}
