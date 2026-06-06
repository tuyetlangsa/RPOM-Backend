using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceTables.CreatePriceTable;

namespace Rpom.Api.Endpoints.Erp.PriceTables;

internal sealed class CreatePriceTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/price-tables",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreatePriceTable.Command(
                    request.Code, request.Name, request.Description,
                    request.BeginDate, request.EndDate, request.IsActive), ct);
                return result.MatchCreated(r => $"/api/price-tables/{r.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceTables")
            .WithName("CreatePriceTable");
    }

    internal sealed record Request(
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive);
}
