using Rpom.Api.Endpoints;
using Rpom.Api.Middleware;

namespace Rpom.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy
                    // NextERP / NextCashier / NextOrder / NextKitchen dev origins.
                    .WithOrigins(
                        "http://localhost:3000",
                        "http://localhost:5173",
                        "http://localhost:5174",
                        "http://localhost:5175")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        services.AddEndpoints(typeof(DependencyInjection).Assembly);
        return services;
    }
}
