using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using Rpom.Application;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Authorization;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;
using Rpom.Infrastructure.Authentication;
using Rpom.Infrastructure.Authorization;
using Rpom.Infrastructure.Clock;
using Rpom.Infrastructure.Database;
using Rpom.Infrastructure.Database.Seeding;
using Rpom.Infrastructure.Outbox;
using CurrentStaffImpl = Rpom.Infrastructure.User.CurrentStaff;

namespace Rpom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddDomainEventHandlers()
            .AddDatabase(configuration)
            .AddHealthChecks(configuration)
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal()
            .AddSeeding(configuration);
    }

    private static IServiceCollection AddSeeding(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BootstrapOptions>(configuration.GetSection(BootstrapOptions.SectionName));
        services.AddSingleton<AccessSeeder>();
        services.AddSingleton<LookupSeeder>();
        services.AddSingleton<ConfigValueSeeder>();
        services.AddSingleton<RoundingConfigSeeder>();
        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<InsertOutboxMessagesInterceptor>();

        services.AddDbContext<IDbContext, ApplicationDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("Database"),
                    npgsqlOptions => npgsqlOptions
                        .EnableRetryOnFailure()
                        .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default)
                        .UseVector())
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<InsertOutboxMessagesInterceptor>()));

        services.Configure<OutboxOptions>(configuration.GetSection("Rpom:Outbox"));
        services.ConfigureOptions<ConfigureProcessOutboxJob>();

        services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentStaff, CurrentStaffImpl>();
        services.AddScoped<IConfigValueService, Configuration.ConfigValueService>();
        services.AddScoped<IVersionService, Versioning.VersionService>();
        services.AddMemoryCache();
        services.AddScoped<IRoundingConfig, Pricing.RoundingConfigService>();
        services.AddScoped<IRoundingCacheInvalidator>(sp =>
            (Pricing.RoundingConfigService)sp.GetRequiredService<IRoundingConfig>());
        services.AddScoped<ICartRecomputeService, Pricing.CartRecomputeService>();
        services.AddScoped<ITicketRecomputeService, Pricing.TicketRecomputeService>();
        services.AddScoped<IRefreshPaymentTotalsService, Pricing.RefreshPaymentTotalsService>();
        services.AddScoped<ITableOperationGuard, Tables.TableOperationGuard>();

        services.AddQuartz(configurator =>
        {
            var scheduler = Guid.NewGuid();
            configurator.SchedulerId = $"default-id-{scheduler}";
            configurator.SchedulerName = $"default-name-{scheduler}";

            // In-memory job store (default). Jobs not persisted across restarts.
            // TODO: switch to UsePersistentStore (Postgres) once migrations create
            // the qrtz_* tables — then jobs survive restart + support clustering.
            //
            // configurator.UsePersistentStore(persistenceOptions =>
            // {
            //     persistenceOptions.UsePostgres(cfg =>
            //         {
            //             cfg.ConnectionString = configuration.GetConnectionString("Database")!;
            //             cfg.TablePrefix = "public.qrtz_";
            //         },
            //         "rpom");
            //     persistenceOptions.UseSystemTextJsonSerializer();
            //     persistenceOptions.UseProperties = true;
            // });
        });

        services.AddQuartzHostedService(options => { options.WaitForJobsToComplete = true; });

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database")!);
        return services;
    }

    private static IServiceCollection AddDomainEventHandlers(this IServiceCollection services)
    {
        var domainEventHandlers = AssemblyReference.Assembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IDomainEventHandler)))
            .ToArray();

        foreach (var domainEventHandler in domainEventHandlers)
        {
            services.TryAddScoped(domainEventHandler);

            var domainEvent = domainEventHandler
                .GetInterfaces()
                .Single(i => i.IsGenericType)
                .GetGenericArguments()
                .Single();

            var closedIdempotentHandler = typeof(IdempotentDomainEventHandler<>).MakeGenericType(domainEvent);

            services.Decorate(domainEventHandler, closedIdempotentHandler);
        }

        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Disable inbound claim mapping: keep raw JWT claim names (e.g. "sub")
                // instead of the legacy mapping to ClaimTypes.NameIdentifier.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = CustomClaims.Username
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddTransient<IClaimsTransformation, CustomClaimsTransformation>();
        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddScoped<IPermissionService, PermissionService>();
        return services;
    }
}
