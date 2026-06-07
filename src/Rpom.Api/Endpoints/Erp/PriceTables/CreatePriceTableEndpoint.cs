using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceTables.CreatePriceTable;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceTables;

internal sealed class CreatePriceTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/price-tables",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreatePriceTable.Response> result = await sender.Send(new CreatePriceTable.Command(
                        request.Code, request.Name, request.Description,
                        request.BeginDate, request.EndDate, request.IsActive), ct);
                    return result.MatchCreated(r => $"/api/price-tables/{r.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceTables")
            .WithName("CreatePriceTable")
            .Produces<ApiResult<CreatePriceTable.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a price table.")
            .WithDescription(
                "Request: JSON body { code:string, name:string, description?:string, beginDate?:date, endDate?:date, isActive:bool }. Response: 201 Created — Location header; JSON body with new price-table id.");
    }

    internal sealed record Request(
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive);
}
