using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceEntries.UpsertPriceEntries;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceEntries;

internal sealed class UpsertPriceEntriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/price-variants/{priceVariantId:int}/entries",
                async (int priceVariantId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var entries = (request.Entries ?? Array.Empty<EntryRequest>())
                        .Select(e => new UpsertPriceEntries.EntryInput(e.ItemId, e.Price, e.IsVatIncluded))
                        .ToList();
                    Result<UpsertPriceEntries.Response> result =
                        await sender.Send(new UpsertPriceEntries.Command(priceVariantId, entries), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceEntries")
            .WithName("UpsertPriceEntries")
            .Produces<ApiResult<UpsertPriceEntries.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Upsert price entries of a price variant.")
            .WithDescription(
                "Request: route priceVariantId (int); JSON body { entries:[{ itemId:int, price:decimal, isVatIncluded:bool }] }. Response: 200 OK — JSON UpsertPriceEntries.Response.");
    }

    internal sealed record Request(IReadOnlyList<EntryRequest>? Entries);

    internal sealed record EntryRequest(int ItemId, decimal Price, bool IsVatIncluded);
}
