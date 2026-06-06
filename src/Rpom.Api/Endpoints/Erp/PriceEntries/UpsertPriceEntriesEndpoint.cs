using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceEntries.UpsertPriceEntries;

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
                var result = await sender.Send(new UpsertPriceEntries.Command(priceVariantId, entries), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceEntries")
            .WithName("UpsertPriceEntries");
    }

    internal sealed record Request(IReadOnlyList<EntryRequest>? Entries);
    internal sealed record EntryRequest(int ItemId, decimal Price, bool IsVatIncluded);
}
