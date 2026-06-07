# CLAUDE.md — Rpom Backend Rules

Đọc file này TRƯỚC khi viết hay sửa code Rpom-backend. Mọi rule trong đây OVERRIDE default behavior.

---

## 0. Communication

- **Chat với dev**: tiếng Việt.
- **Code / identifiers / comments**: tiếng Anh.
- **Doc artifact**: tiếng Anh.
- **Trả lời ngắn gọn**: bullet, không giảng đạo, không kể lể quá trình.
- **Trung thực**: không suy đoán API/SP/cột mà chưa verify. Nếu chưa thấy nguồn → nói thẳng "tôi chưa biết, cần share source".
- **Bị cãi**: nếu dev push back, đọc lại file/source thật trước khi đồng ý hoặc bảo vệ lập luận. Cite `file:line`. Không capitulate khi có evidence; không cố chấp khi sai.

---

## 1. Stack & Architecture

- **ASP.NET Core 10** + EF Core + Postgres (pgvector cho RAG).
- **Pragmatic Clean Architecture + CQRS via MediatR.**
- Dependency:
  ```
  Domain         (no refs)
     ↑
  Application    (→ Domain)
     ↑
  Infrastructure (→ Application, Domain)
     ↑
  Api / Worker   (→ Application, Infrastructure)
  ```
- **No separate Contracts project.** Endpoint Request/Response records nested inside endpoint file. Handler Command/Query/Response nested inside handler file.

---

## 2. CQRS Handler Pattern — PER USE CASE (BLOCKING)

**MỖI use case = 1 file**, chứa:
- `Command(...)` hoặc `Query(...)` record
- `Response(...)` record **nested in same file, per use case** — NEVER shared across use cases
- `Handler : ICommandHandler<,>` hoặc `IQueryHandler<,>`
- `Validator : AbstractValidator<Command>` (nếu cần)

**CẤM**:
- ❌ Suffix `Dto`, `Model`, `ViewModel` — chỉ dùng `Response`, `Request`, `Command`, `Query`.
- ❌ Shared response record cho List/Get/Create/Update của cùng aggregate. Mỗi use case có response RIÊNG, dù schema giống nhau 95%.
- ❌ Folder `Dtos/` hoặc `Models/` chứa shared types cross-usecase.

**Đúng**:
```csharp
// src/Rpom.Application/Tickets/GetTicketDetails/GetTicketDetails.cs
public static class GetTicketDetails
{
    public sealed record Query(long TicketId) : IQuery<Response>;

    public sealed record Response(
        Info TicketInfo,
        IReadOnlyList<ItemDetail> ItemDetails,
        IReadOnlyList<OrderBatch> OrderedItems,
        IReadOnlyList<OrderingItem> OrderingItems,
        Payment Payment);

    public sealed record Info(long TicketId, string TicketCode, ...);
    public sealed record ItemDetail(long Id, int ItemId, string ItemName, ...);
    public sealed record OrderBatch(long OrderId, int OrderNumber, ...);
    public sealed record OrderingItem(long CartItemId, int ItemId, ...);
    public sealed record Payment(decimal TotalAmount, decimal PaidAmount, ...);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query q, CancellationToken ct) { ... }
    }
}
```

**Sai**:
```csharp
// ❌ Shared DTO file
public record TicketDto(long Id, ...);   // dùng cho cả List + Get → sai

// ❌ Generic suffix
public record TicketDetailDto(...);      // sai — dùng "Response"

// ❌ External file
public record TicketResponse(...);       // ngoài folder usecase → sai
```

---

## 3. Endpoint Pattern (BLOCKING)

Endpoint dùng **convention-based discovery**, KHÔNG có controller.

1. Class implement `Rpom.Api.Endpoints.IEndpoint`, dưới folder **persona** (`Endpoints/Erp/`, `Endpoints/Cashier/`, `Endpoints/Order/`, `Endpoints/Kitchen/`, `Endpoints/Guest/`, `Endpoints/System/`).
2. `MapEndpoint(IEndpointRouteBuilder app)`:
   - `app.MapGet/Post/Put/Delete(...)`
   - `.WithTags("<Aggregate>")` — tag theo **aggregate**, KHÔNG theo persona (vd `"Tickets"`, `"Items"`)
   - `.RequireAuthorization(Permissions.<Code>)` cho mọi endpoint auth'd
   - Send `Query/Command` qua `ISender` (MediatR)
   - Convert `Result<T>` qua extension methods ở `Rpom.Api.Results`
3. **KHÔNG manual register** — `EndpointExtensions.AddEndpoints()` scan assembly tự động.

```csharp
internal sealed class GetTicketDetailsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cashier/tickets/{ticketId:long}",
            async (long ticketId, ISender sender) =>
            {
                var result = await sender.Send(new GetTicketDetails.Query(ticketId));
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.CashierViewTicket)
            .WithTags("Tickets")
            .WithName("GetTicketDetails");
    }
}
```

---

## 4. Result<T> & Errors

- Handler **KHÔNG throw** cho expected failure. Dùng `Result.Failure(error)` / `Result.Success(value)`.
- Errors định nghĩa per aggregate trong `<Aggregate>Errors.cs` ở `Rpom.Domain/`.
- Naming error: `<Aggregate>.<Reason>` (vd `Ticket.NotFound`, `PriceVariant.OverlapConflict`).
- Endpoint convert qua `Match*` extensions: `MatchOk`, `MatchCreated`, `MatchNoContent`.
- Throw exception **CHỈ** cho: infrastructure failure (DB down), programmer bug (null khi không thể null), validation behavior (FluentValidation throws → pipeline catch).

---

## 5. Naming Convention (BLOCKING)

### Tables + Fields
- **PascalCase**, NO Hungarian prefix, NO underscore-between-words.
- `Id` PK, `<Entity>Id` FK suffix (vd `CounterId`).
- Date thuần: suffix `*Date` (`BeginDate`, `EndDate`). Type `date`.
- Timestamp: suffix `*At` (`CreatedAt`, `UpdatedAt`, `ClosedAt`). Type `datetime2(3)` (Postgres `timestamp(3)`).
- Bool: prefix `Is*` / `Has*` (`IsActive`, `HasRecipe`).
- Money: `decimal(18,2)` cho final, `decimal(18,5)` cho intermediate compute.
- Percent: `decimal(5,2)` (cho phép tối đa 999.99%).

### Audit columns
- Master: `CreatedAt`, `UpdatedAt`, `IsActive`.
- Transactional: `CreatedAt`, `UpdatedAt`, `Version` int (nếu cần optimistic concurrency).
- Append-only: chỉ `CreatedAt`.
- Junction: chỉ `CreatedAt`.
- **KHÔNG có** `CreatedById` / `UpdatedById` ở bất kỳ table — actor lưu trong `AuditLog`.

### Soft delete
- `IsActive bool default true` trên master tables.
- Operational status (Ticket, Order, Reservation, ...) dùng enum varchar(20) (`OPEN`, `CLOSED`, `CANCELLED`, ...).

---

## 6. Permissions (BLOCKING)

- **Flat string permissions** (vd `ticket:view_cashier`, `item:create`).
- Catalog ở `Rpom.Application/Access/Permissions.cs` (const string).
- Seed vào DB qua `AccessSeeder`.
- Endpoint: `.RequireAuthorization(Permissions.<Code>)`.
- `PermissionAuthorizationPolicyProvider` tự tạo policy → `PermissionAuthorizationHandler` check claim.

### Permission lifecycle
1. Login (`POST /api/auth/login`) → BCrypt verify → JWT chỉ chứa `sub` (StaffAccountId). **KHÔNG embed permissions trong JWT.**
2. Per request → `CustomClaimsTransformation` chạy → query `staff_account_permissions` → augment ClaimsPrincipal với `permission` claims.
3. **Counter context KHÔNG ở JWT.** Cashier open `CashDrawerSession` (POST `/api/cash-drawers`) bound to `(StaffAccountId, CounterId)`. Operational endpoint lookup OPEN session qua `ICurrentStaff` runtime.

### Role
- Label only — KHÔNG carry permissions trong schema.
- Dùng làm default permission template lúc tạo StaffAccount.
- System roles: Owner, Manager, Cashier, Order Staff, Kitchen Staff, Admin Vendor — KHÔNG delete được (`IsSystemRole=true`).

### Permission Group
- UI grouping only — không phải runtime auth.

---

## 7. Domain Events + Outbox

- Aggregate raise event qua `entity.Raise(new XxxDomainEvent(...))`.
- `InsertOutboxMessagesInterceptor` persist event vào `OutboxMessages` table khi `SaveChangesAsync`.
- Quartz `ProcessOutboxJob` dispatch event.
- Handler decorate `IdempotentDomainEventHandler<>` → tracked qua `OutboxMessageConsumers`, re-delivery safe.
- Implement `IDomainEventHandler<TEvent>` ở `Rpom.Infrastructure` — auto-discovered.

---

## 8. AuditLog (BLOCKING)

- **1 bảng polymorphic** `AuditLog`, KHÔNG FK.
- Schema: `Id`, `EntityType`, `EntityId`, `Action`, `ActorStaffAccountId`, `ActorFullName`, `Timestamp`, `Summary`.
- Action: `CREATE`, `UPDATE`, `DELETE` + business action (`REOPEN`, `APPROVE`, `VOID`, `OPEN_SHIFT`, `CLOSE_SHIFT`, ...).
- Handler ghi `AuditLog` trực tiếp: `db.AuditLogs.Add(new AuditLog { ... })` trong cùng `SaveChangesAsync` với business mutation.
- **KHÔNG có** `IAuditLogger` abstraction — inline cho gọn.
- **KHÔNG ghi AuditLog cho**: read-only query, derived data (vd `TicketItemSum`), per-row poll cursor update.

---

## 9. DomainVersion Bump (BLOCKING)

Sau `SaveChangesAsync` thành công, **mọi Command handler relevant phải bump scope** qua `IVersionService.BumpAsync`.

```csharp
await db.SaveChangesAsync(ct);
await versionService.BumpAsync(VersionScopes.Menu, $"Item.Create(id={entity.Id})", ct);
```

Catalog scope: `MENU`, `PRICING`, `FLOOR_PLAN`, `KITCHEN`, `ACCESS`, `CONFIG`.

Quy tắc bump đầy đủ ở `docs/RPOM_Versioning_Strategy.md` §6.

**Quy tắc CẤM**:
- ❌ Bump TRƯỚC `SaveChangesAsync` (nếu rollback thì version đã tăng — sai).
- ❌ Bump scope KHÔNG liên quan để "force refresh" — sai.
- ❌ Expose endpoint POST/PUT để bump thủ công — chỉ qua handler.

---

## 10. Concurrency Strategy

Per `docs/RPOM_Versioning_Strategy.md` §17 — 3 cơ chế:

| Cơ chế | Khi dùng | Cost |
|---|---|---|
| **Optimistic `Version` int** + EF `IsConcurrencyToken` | Single-row edit có thể conflict | Rẻ — không lock |
| **SELECT FOR UPDATE** trong transaction | Multi-step check-act đa bảng (`SendToKitchen`, recompute) | Trung — lock hết transaction |
| **UPSERT** 1 statement (`INSERT ON CONFLICT DO UPDATE`) | Counter increment đơn giản | Rẻ nhất |

Map scenario → cơ chế ở Versioning Spec §17.6. Tham khảo trước khi build feature.

**Anti-patterns**:
- ❌ `SELECT FOR UPDATE` cho counter increment.
- ❌ Optimistic Version cho multi-step check-act đa bảng.
- ❌ Catch `DbUpdateConcurrencyException` rồi retry vô hạn.
- ❌ Bump `Version` thủ công — EF tự handle qua `IsConcurrencyToken`.

---

## 11. Snapshot Pattern (BLOCKING cho transactional lines)

OrderItem, CartItem, TicketItemSum, TicketPaymentDetail PHẢI snapshot:
- `ItemCode`, `ItemName` từ Item lúc add (audit-immutable).
- `UomCode`, `UomName` từ Uom lúc add.
- `UnitPrice` từ PriceEntry (pre-VAT pre-SC).
- `VatPercent` từ Item.
- `ServiceChargePercent`, `ServiceChargeVatPercent` từ Ticket (đã snapshot từ Area lúc open).

Snapshot field **KHÔNG đổi** khi master Item rename/reprice. Bill cũ luôn render đúng thông tin tại thời điểm order.

---

## 12. Money / Rounding (BLOCKING)

Đọc `docs/superpowers/specs/2026-06-06-pricing-billing-spec.md` trước khi đụng pricing.

- **`UnitPrice` luôn pre-VAT, pre-SC** trên CartItem/OrderItem. KHÔNG lưu cờ `IsVatIncluded` ở line — chỉ ở `PriceEntry` (config gốc).
- **Normalize tại GetMenu API**: BE compute `BasePrice` (pre-VAT) + `DisplayPrice` (all-in) trả về FE. FE submit `BasePrice` khi add cart.
- **Rounding precision** qua `RoundingConfig` table + `IRoundingConfig.GetDigits(keyCode)` helper. KHÔNG hardcode `Math.Round(value, N)` — luôn `Money.Round(value, rc, "I_ROUNDXXX")`.
- **Rounding mode hardcode**: `MidpointRounding.AwayFromZero`. KHÔNG configurable v1.
- **Discount 1 cấp**: Line + Ticket cùng compute, cái lớn thắng, cái thua zero.
- **SC tính trên LineSubtotal GỐC** (trước Discount). VAT items tính trên `LineSubtotal − Discount`. VAT của SC riêng (`Area.ServiceChargeVatPercent`).
- **`Ticket.RoundingAdjustment`** lưu sai số làm tròn = `TotalAmount − (Subtotal − Discount + SC + VAT)`.

---

## 13. Recompute Service (BLOCKING)

- **Eager** — mọi mutation gọi `Recompute` trong cùng transaction handler.
- `TicketRecomputeService.RecomputeAsync(ticketId)` cho mọi mutation cấp Ticket (OrderItem, Discount, Transfer, Merge, ...).
- `CartRecomputeService.RecomputeAsync(orderId)` cho mọi mutation cấp Cart (CartItem, CartItemDetail).
- `RefreshPaymentTotalsService.RefreshAsync(ticketId)` cho mutation Payment — KHÔNG full recompute.
- Trigger matrix ở pricing-billing-spec §5. Mọi handler tương ứng PHẢI gọi service tương ứng.

---

## 14. Snapshot Pattern Vs Lookup

- **Snapshot**: Cart/Order/Ticket/Payment lines lưu copy của Item/Uom/Modifier/Area config tại thời điểm add. Audit-immutable.
- **Lookup**: GetMenu / GetFloorPlan lookup runtime, không snapshot.
- **Ticket header snapshot khi Open**: `ServiceChargePercent`, `ServiceChargeVatPercent` từ Area lúc open ticket. Khi Transfer Table → re-snapshot và recompute.

---

## 15. Common Commands

```bash
dotnet build
dotnet test
dotnet run --project src/Rpom.Api
dotnet ef migrations add <Name> --project src/Rpom.Infrastructure --startup-project src/Rpom.Api
dotnet ef database update --project src/Rpom.Infrastructure --startup-project src/Rpom.Api
docker compose up --build
```

---

## 16. Project Layout

```
src/
├── Rpom.Domain/                  — entity per aggregate folder (Access/Restaurant/Menu/...)
│   ├── Common/                   — Entity, AggregateRoot, Result<T>, Error, DomainEvent, OutboxMessage
│   ├── <Aggregate>/              — entities + <Aggregate>Errors.cs
│   └── ...
├── Rpom.Application/
│   ├── Abstraction/              — IDbContext, IPermissionService, ICurrentStaff, IDateTimeProvider, IVersionService, IRoundingConfig, ...
│   ├── Behaviors/                — Validation, Logging, ExceptionHandling pipeline
│   ├── Common/                   — Money, Page<T>, ...
│   └── <Aggregate>/<UseCase>/<UseCase>.cs — 1 file per use case
├── Rpom.Infrastructure/
│   ├── Database/                 — ApplicationDbContext, EF configs, migrations, seeders
│   ├── Authentication/           — JWT, BCrypt, permission claims
│   ├── Versioning/               — VersionService implementation
│   ├── Money/                    — RoundingConfig cache, Money helper impl
│   └── ...
├── Rpom.Api/
│   ├── Endpoints/<Persona>/<Aggregate>/<UseCase>Endpoint.cs
│   ├── Results/                  — Result<T> → HTTP extensions
│   └── Program.cs
└── Rpom.Worker/                  — Quartz jobs (cron, outbox dispatch)

tests/
├── Rpom.Domain.UnitTests/
├── Rpom.Application.UnitTests/
└── Rpom.Api.IntegrationTests/
```

---

## 17. Migration Workflow

1. Thay đổi entity / EF config.
2. `dotnet ef migrations add <DescriptiveName>`.
3. Review file `*.cs` generated — check up/down đúng. Nếu add NOT NULL với default → kiểm tra dữ liệu cũ.
4. Run `dotnet build` đảm bảo migration compile.
5. Smoke test local DB (drop schema + migrate).
6. KHÔNG commit migration nếu chưa apply local.

---

## 18. Testing

- Unit test domain logic (entity invariant, value object) — KHÔNG hit DB.
- Unit test application handler — mock `IDbContext` qua in-memory hoặc `IDbContextFactory<TestDbContext>`.
- Integration test endpoint qua `WebApplicationFactory<Program>` + Postgres test container (Testcontainers).
- Smoke test sau khi merge feature lớn — chạy seed script + call endpoint qua curl.

---

## 19. Git Workflow

- **Branch**: feature branch per use case nhỏ, hoặc per feature lớn cho phase nhiều use case.
- **Commit message** — author `tuyetlangsa <longheo242@gmail.com>`, KHÔNG Co-Authored-By Claude line:
  - Title: ≤ 70 chars, imperative.
  - Body: max 3-5 bullets, focus "why" hơn "what".
  - KHÔNG enumerate file.
- **Tuyệt đối KHÔNG**: `--no-verify`, `--no-gpg-sign`, `--amend` (trừ khi dev request), force push to main.
- **KHÔNG push** lên remote trừ khi dev explicit.
- KHÔNG commit `.env`, credentials.

---

## 20. Anti-patterns CẤM

- ❌ Suffix `Dto`, `Model`, `ViewModel` trên record/class.
- ❌ Shared response cho List/Get/Create/Update cùng aggregate.
- ❌ Folder `Dtos/`, `Models/` chứa shared types.
- ❌ Throw exception cho expected failure (dùng `Result.Failure`).
- ❌ `CreatedById` / `UpdatedById` cột trên entity table (dùng AuditLog).
- ❌ Embed permissions vào JWT.
- ❌ Embed Counter context vào JWT (dùng `CashDrawerSession` runtime lookup).
- ❌ Manual register endpoint trong `Program.cs` (convention-based discovery).
- ❌ `WithTags("<persona>")` ở endpoint (dùng aggregate).
- ❌ Bump `DomainVersion` trước `SaveChangesAsync`.
- ❌ Hardcode `Math.Round(value, N)` trong recompute (dùng `Money.Round(value, rc, "key")`).
- ❌ Mix optimistic + pessimistic lock trên cùng row.
- ❌ Catch `DbUpdateConcurrencyException` + retry vô hạn.
- ❌ Generic comment kiểu "// updated by handler" trên field (chỉ comment WHY).
- ❌ Bê F2 patterns blindly — luôn cherry-pick + simplify. Dropped list ở pricing-billing-spec §1.

---

## 21. Source-of-truth Docs

| Topic | File |
|---|---|
| Glossary, entities, business rules | `~/CapstoneProject/docs/RPOM_Glossary.md` |
| Logical ERD | `~/CapstoneProject/docs/RPOM_Logical_ERD.md` + `.dbml` (v0.18+ — 51 tables across 9 areas) |
| Versioning & polling | `~/CapstoneProject/docs/RPOM_Versioning_Strategy.md` |
| Pricing model | `~/CapstoneProject/docs/RPOM_Pricing_Spec.md` |
| Cashier pricing/billing fields | `~/CapstoneProject/docs/superpowers/specs/2026-06-06-pricing-billing-spec.md` |
| Cashier read APIs design | `~/CapstoneProject/docs/superpowers/specs/2026-06-06-cashier-apis-design.md` |
| Business flows F1-F7, E1-E4 | `~/CapstoneProject/docs/RPOM_Business_Flows.md` |
| KF1 staff scheduling user stories | `~/CapstoneProject/docs/KF1_StaffScheduling_UserStories_UseCases.md` |
| KF2 sales operation user stories | `~/CapstoneProject/docs/KF2_SalesOperation_UserStories_UseCases.md` |
| F2 reference (cherry-pick patterns only) | `~/CapstoneProject/docs/table definitions.sql` + memory `feedback_f2_reference` |

**LUÔN đọc docs liên quan TRƯỚC khi implement.** Đừng đoán business rule từ shape của code. Memory `feedback_read_docs_first` đã ghi rõ.

---

## 22. Context-specific Reminders

- **Capstone scope, single-tenant, VND only.** Mọi feature multi-currency/multi-warehouse/multi-tenant đã drop.
- **Polling, không WebSocket.** Aggregate version + per-row UpdatedAt cursor.
- **AuditLog không có viewer screen riêng** — panel embed trên màn detail của entity.
- **Reservation không tạo placeholder ticket** — hold derived từ time + buffer config.
- **Discount 1 cấp** (cái lớn thắng), không cộng dồn.
- **Không có tip, deposit, excise tax, hourly billing** — defer v2.
- **Modifier IS an Item** (Glossary §3.9) — không có table riêng Modifier.

---

## 23. Khi Sửa Hay Mở Rộng Rule

Cập nhật file này TRƯỚC khi code. Memory sẽ stale, file này là source of truth.

Đổi rule mà chưa update CLAUDE.md → re-apply rule cũ.
