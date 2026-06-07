using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;

namespace Rpom.Api.Extensions;

public static class SwaggerExtensions
{
    internal static IServiceCollection AddSwaggerGenWithAuth(this IServiceCollection services)
    {
        services.AddSwaggerGen(o =>
        {
            o.CustomSchemaIds(id => id.FullName!.Replace('+', '-'));

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "JWT Authentication",
                Description = "Enter your JWT token in this field",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT"
            };

            o.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);

            // Microsoft.OpenApi 2.x + Swashbuckle 10.x: AddSecurityRequirement takes a
            // factory Func<OpenApiDocument, OpenApiSecurityRequirement>. The inner
            // OpenApiSecuritySchemeReference carries the scheme Id.
            o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
            {
                {
                    // Pass the host document so the reference resolves to the registered
                    // "Bearer" scheme. Without it the requirement serializes to an empty
                    // object ([{}]) and SwaggerUI never attaches the Authorization header.
                    new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, doc),
                    []
                }
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerWithUi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        return app;
    }
}
