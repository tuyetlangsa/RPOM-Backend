using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Inventory;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Infrastructure.Authentication;
using Rpom.Infrastructure.Authorization;
using Rpom.Infrastructure.Clock;
using Rpom.Infrastructure.Configuration;
using Rpom.Infrastructure.Database;
using Rpom.Infrastructure.Database.Seeding;
using Rpom.Infrastructure.Outbox;
using Rpom.Infrastructure.Pricing;
using Rpom.Infrastructure.Tables;
using Rpom.Infrastructure.Versioning;
using Rpom.Infrastructure.Inventory;
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
            .AddPayments(configuration)
            .AddSeeding(configuration);
    }

    private static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Payments.SePayOptions>(
            configuration.GetSection(Payments.SePayOptions.SectionName));
        services.AddScoped<
            Application.Abstraction.Payments.IQrPaymentGateway,
            Payments.SePayQrPaymentGateway>();
        return services;
    }

    private static IServiceCollection AddSeeding(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BootstrapOptions>(configuration.GetSection(BootstrapOptions.SectionName));
        services.AddSingleton<AccessSeeder>();
        services.AddSingleton<LookupSeeder>();
        services.AddSingleton<CashierDemoSeeder>();
        services.AddSingleton<ConfigValueSeeder>();
        services.AddSingleton<RoundingConfigSeeder>();
        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<InsertOutboxMessagesInterceptor>();
        services.TryAddSingleton<ConcurrencyVersionInterceptor>();

        services.AddDbContext<IDbContext, ApplicationDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("Database"),
                    npgsqlOptions => npgsqlOptions
                        .EnableRetryOnFailure()
                        .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default)
                        .UseVector())
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    sp.GetRequiredService<InsertOutboxMessagesInterceptor>(),
                    sp.GetRequiredService<ConcurrencyVersionInterceptor>()));

        services.Configure<OutboxOptions>(configuration.GetSection("Rpom:Outbox"));
        services.ConfigureOptions<ConfigureProcessOutboxJob>();

        services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentStaff, CurrentStaffImpl>();
        services.AddScoped<IConfigValueService, ConfigValueService>();
        services.AddScoped<IVersionService, VersionService>();
        services.AddMemoryCache();
        services.AddScoped<IRoundingConfig, RoundingConfigService>();
        services.AddScoped<IRoundingCacheInvalidator>(sp =>
            (RoundingConfigService)sp.GetRequiredService<IRoundingConfig>());
        services.AddScoped<IMenuPriceResolver, MenuPriceResolver>();
        services.AddScoped<ICartRecomputeService, CartRecomputeService>();
        services.AddScoped<ITicketRecomputeService, TicketRecomputeService>();
        services.AddScoped<IRefreshPaymentTotalsService, RefreshPaymentTotalsService>();
        services.AddScoped<ITableOperationGuard, TableOperationGuard>();
        services.AddScoped<IStockMovementService, StockMovementService>();

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
        Type[] domainEventHandlers = AssemblyReference.Assembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IDomainEventHandler)))
            .ToArray();

        foreach (Type domainEventHandler in domainEventHandlers)
        {
            services.TryAddScoped(domainEventHandler);

            Type domainEvent = domainEventHandler
                .GetInterfaces()
                .Single(i => i.IsGenericType)
                .GetGenericArguments()
                .Single();

            Type closedIdempotentHandler = typeof(IdempotentDomainEventHandler<>).MakeGenericType(domainEvent);

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

        JwtOptions jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;

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
