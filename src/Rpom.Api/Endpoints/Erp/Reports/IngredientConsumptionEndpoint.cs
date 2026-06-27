using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reports;
using Rpom.Application.Reports.IngredientConsumption;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class IngredientConsumptionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/ingredient-consumption",
                async (DateTime? fromDate, DateTime? toDate, int? itemId,
                    int? counterId, ISender sender, CancellationToken ct) =>
                {
                    var filter = new ReportFilter(fromDate, toDate, counterId, null, null, null);
                    var result = await sender.Send(new IngredientConsumption.Query(filter, itemId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReportItemConsumption)
            .WithTags("Reports").WithName("IngredientConsumption")
            .Produces<ApiResult<IReadOnlyList<IngredientConsumption.Response>>>()
            .WithSummary("Ingredient consumption via BOM over date range");
    }
}
