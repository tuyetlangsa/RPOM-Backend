using Microsoft.Extensions.DependencyInjection;
using Rpom.Api;
using Rpom.Api.Endpoints;
using Rpom.Api.Extensions;
using Rpom.Api.Middleware;
using Rpom.Application;
using Rpom.Infrastructure;
using Rpom.Infrastructure.Database.Seeding;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddSwaggerGenWithAuth();

builder.Services
    .AddApplication(builder.Configuration)
    .AddPresentation()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUi();
}

app.ApplyMigrations();

// Seed Access aggregate (PermissionGroups, Permissions, Roles, bootstrap Owner).
// Idempotent — safe to run on every startup.
await using (var scope = app.Services.CreateAsyncScope())
{
    var accessSeeder = scope.ServiceProvider.GetRequiredService<AccessSeeder>();
    await accessSeeder.SeedAsync();

    var lookupSeeder = scope.ServiceProvider.GetRequiredService<LookupSeeder>();
    await lookupSeeder.SeedAsync();

    var configSeeder = scope.ServiceProvider.GetRequiredService<ConfigValueSeeder>();
    await configSeeder.SeedAsync();

    var roundingConfigSeeder = scope.ServiceProvider.GetRequiredService<RoundingConfigSeeder>();
    await roundingConfigSeeder.SeedAsync();
}

app.UseCors();
app.UseLogContext();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapEndpoints();

app.Run();
