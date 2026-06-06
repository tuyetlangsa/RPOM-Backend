using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceTables.UpdatePriceTable;

namespace Rpom.Api.Endpoints.Erp.PriceTables;

internal sealed class UpdatePriceTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/price-tables/{id:int}",
            async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new UpdatePriceTable.Command(
                    id, request.Code, request.Name, request.Description,
                    request.BeginDate, request.EndDate, request.IsActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceTables")
            .WithName("UpdatePriceTable");
    }

    internal sealed record Request(
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive);
}
