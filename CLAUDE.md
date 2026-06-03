# CLAUDE.md

Architecture + conventions for Rpom backend. Read this first when implementing or modifying the codebase.

## What Rpom is

Restaurant POS and Operations Management Platform — full-service mid-range Vietnamese restaurants. Backend serves 4 frontends from a single API:

- **NextERP** *(Next.js)* — Owner / Manager / Admin web app: master data, reports, AI Operations Assistant
- **NextCashier** *(React + Vite)* — Cashier app: payment, shift open/close
- **NextOrder** *(React + Vite)* — Order Staff app: floor plan, take orders
- **NextKitchen** *(React + Vite)* — Kitchen Display System (KDS)
- **NextGuest** *(future)* — anonymous QR self-order

Source-of-truth docs:
- `~/CapstoneProject/docs/RPOM_Logical_ERD.md` + `.dbml` (latest v0.18 — 51 tables across 9 areas)
- `~/CapstoneProject/docs/RPOM_Glossary.md` (entities, business rules)
- `~/CapstoneProject/docs/RPOM_AI_Operations_Assistant_V1.md`

## Common commands

```bash
dotnet build
dotnet test
dotnet run --project src/Rpom.Api
dotnet ef migrations add <Name> --project src/Rpom.Infrastructure --startup-project src/Rpom.Api
dotnet ef database update --project src/Rpom.Infrastructure --startup-project src/Rpom.Api
docker compose up --build
```

## Architecture

Pragmatic Clean Architecture + CQRS via MediatR. Dependency direction:

```
Domain (no refs)
   ↑
Application (→ Domain)
   ↑
Infrastructure (→ Application, → Domain)
   ↑
Api (→ Application, Infrastructure)
Worker (→ Application, Infrastructure)
```

**Pragmatic deviations from strict Clean:**
- Domain may reference `Pgvector` (for `RagDocumentChunk.Embedding`) — DB-specific type leak accepted for simplicity.
- No separate `Contracts` project for DTOs — endpoint Request/Response records are nested in endpoint file.

### Project roles

- **Rpom.Domain** — entities organized by aggregate (`Access`, `Restaurant`, `Menu`, `Operations`, `Sales`, `Reservation`, `Inventory`, `Ai`, `Audit`). `Common/` has shared `Entity`, `AggregateRoot`, `Result<T>`, `Error`, `DomainEvent`, `OutboxMessage`.
- **Rpom.Application** — MediatR handlers, FluentValidation validators, abstractions (`IDbContext`, `IPermissionService`, `ICurrentStaff`, `IDateTimeProvider`, `IAiAgent`, `IRagSearchService`, `IAuditLogger`), pipeline behaviors (`ValidationBehavior`, `LoggingBehavior`, `ExceptionHandlingBehavior`).
- **Rpom.Infrastructure** — EF Core (`ApplicationDbContext`, pgvector via Npgsql), migrations, JWT bearer, BCrypt password hashing, Quartz scheduler (Postgres-persisted), outbox (`InsertOutboxMessagesInterceptor` + `ProcessOutboxJob`), `AuditLogger` (centralized polymorphic AuditLog writer), AI services.
- **Rpom.Api** — Minimal API endpoints organized by **persona folder** (`Endpoints/Erp/`, `Endpoints/Order/`, `Endpoints/Cashier/`, `Endpoints/Kitchen/`, `Endpoints/Guest/`, `Endpoints/System/`). Swagger tags by **aggregate** (`.WithTags("Tickets")`, `.WithTags("Items")`...). Serilog request logging.
- **Rpom.Worker** — Quartz background jobs (AI low-stock cron, EOD summary cron, reservation NO_SHOW finalize cron, RAG re-index cron).

## Endpoint pattern (CRITICAL — every new route follows this)

Endpoints use convention-based discovery — NOT controllers.

1. Create class implementing `Rpom.Api.Endpoints.IEndpoint` under appropriate persona folder.
2. In `MapEndpoint(IEndpointRouteBuilder app)`:
   - Call `app.MapGet/Post/Put/Delete(...)`
   - `.WithTags("<Aggregate>")` — aggregate-based tag, NOT persona
   - `.RequireAuthorization(Permissions.<Code>)` — required for all auth'd endpoints
   - Send `Command` / `Query` via `ISender` (MediatR)
   - Use `Result<T>` extension methods from `Rpom.Api.Results` to convert to HTTP
3. **NO manual registration** — `EndpointExtensions.AddEndpoints()` scans assembly; `MapEndpoints()` calls each at startup.

Example skeleton:
```csharp
internal sealed class CreateItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("items", async ([FromBody] Request request, ISender sender) =>
        {
            Result<Guid> result = await sender.Send(new CreateItem.Command(request.Name, ...));
            return result.MatchCreated(id => $"/items/{id}");
        })
        .RequireAuthorization(Permissions.CreateItem)
        .WithTags("Items")
        .WithName("CreateItem");
    }

    internal sealed record Request(string Name, /* ... */);
}
```

## CQRS handler pattern

Each use case = 1 file with nested `Command/Query` record, `Response` record, `Handler : ICommandHandler<,>` / `IQueryHandler<,>` (defined in `Rpom.Application/Abstraction/Messaging/`), and `Validator : AbstractValidator<Command>`.

Handlers:
- Resolve current user via `ICurrentStaff.StaffAccountId`
- Mutate via `IDbContext`
- Raise domain events via `entity.Raise(new XxxDomainEvent(...))`
- Return `Result<T>` / `Result` — NEVER throw for expected failures, use `Errors` constants

## Permission flow (custom — not Identity / Auth0)

1. **Login** *(POST /api/auth/login)*: verify BCrypt → issue JWT with only `sub` claim (StaffAccountId). Permissions NOT baked into JWT.
2. **Per request**: `CustomClaimsTransformation` runs after JWT validation → calls `IPermissionService.GetPermissionsAsync(staffAccountId)` → query `staff_account_permissions` table → augment `ClaimsPrincipal` with `permission` claims.
3. **Endpoint check**: `.RequireAuthorization("ticket:reopen")` → `PermissionAuthorizationPolicyProvider` auto-creates policy → `PermissionAuthorizationHandler` checks `claims.Contains("ticket:reopen")`.
4. **Counter selection**: separate endpoint `POST /api/auth/select-counter` re-issues JWT with `counterId` claim added. Operational endpoints scope by `counterId` claim.

Permissions are flat strings (e.g. `ticket:reopen`, `item:create`, `report:revenue`). Catalog defined in `Rpom.Api/Permissions.cs` (const strings) + seeded into DB.

`PermissionGroup` is **UI grouping only** — not part of runtime auth (just buckets permissions in the picker UI).
`Role` is a **label** on StaffAccount — does NOT carry permissions. Used for default permission template on account creation (defaults coded in seeder).

## Domain events + outbox

Domain events raised on aggregates are persisted to `OutboxMessages` table by `InsertOutboxMessagesInterceptor` during `SaveChangesAsync`. Quartz `ProcessOutboxJob` dispatches them. Each handler decorated with `IdempotentDomainEventHandler<>` so re-delivery is safe (tracked in `OutboxMessageConsumers`).

When adding a new domain event handler: implement `IDomainEventHandler<TEvent>` in `Rpom.Infrastructure` — auto-discovered.

## Audit log (centralized polymorphic)

`AuditLog` table is single source of truth for "who did what". Schema:
- `Id`, `EntityType`, `EntityId` (polymorphic, NO FK), `Action` (CREATE/UPDATE/DELETE + business actions like REOPEN, APPROVE, VOID)
- `ActorStaffAccountId`, `ActorFullName` (snapshot)
- `Timestamp`, `Summary` (optional human-readable)

**Entity tables do NOT have `CreatedById` / `UpdatedById` columns** — only `CreatedAt` / `UpdatedAt`. Actor info lives in AuditLog.

Use `IAuditLogger.LogAsync(entityType, entityId, action, summary)` from handlers — NEVER write to AuditLog directly via DbContext.

## Conventions

- **Naming**: PascalCase tables + fields, `Id` PK, `<Entity>Id` FK suffix, `IsActive`/`HasRecipe` bool prefix, `*Date` for `date`, `*At` for `datetime2(3)` timestamps (`CreatedAt`, `UpdatedAt`).
- **Polling-friendly**: `UpdatedAt datetime2(3)` on Ticket, Order, CartItem, OrderItem, Reservation, ItemStock, AiNotification, Table — used as poll cursor (`WHERE UpdatedAt > @since`).
- **Version int**: Optimistic concurrency on Ticket + ScheduleAssignment + ShiftSession only.
- **Snapshot fields**: OrderItem stores ItemCode/ItemName/UnitPrice copied from Item at order time — preserves historical accuracy if Item renamed/repriced.
- **Result pattern**: Handlers never throw for expected failures; use `Result.Failure(SomeError)`. Errors live in `<Aggregate>Errors.cs` per aggregate.
- **Cancellation tokens**: every async method takes `CancellationToken ct` last.
- **internal sealed**: default visibility for handlers/endpoints; expose `public` only when consumed across project boundaries.
