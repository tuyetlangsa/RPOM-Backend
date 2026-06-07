using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceTables.UpdatePriceTable;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceTables;

internal sealed class UpdatePriceTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/price-tables/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdatePriceTable.Response> result = await sender.Send(new UpdatePriceTable.Command(
                        id, request.Code, request.Name, request.Description,
                        request.BeginDate, request.EndDate, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceTables")
            .WithName("UpdatePriceTable")
            .Produces<ApiResult<UpdatePriceTable.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a price table.")
            .WithDescription(
                "Request: route id (int); JSON body { code:string, name:string, description?:string, beginDate?:date, endDate?:date, isActive:bool }. Response: 200 OK — JSON UpdatePriceTable.Response.");
    }

    internal sealed record Request(
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive);
}
