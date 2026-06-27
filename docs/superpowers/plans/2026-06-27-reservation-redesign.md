# Reservation Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the reservation feature (phone bookings) per `docs/superpowers/specs/2026-06-27-reservation-redesign-design.md`: multi-table bookings, non-blocking hold, lazy no-show, counter-scoped, seat opens one ticket per selected table.

**Architecture:** Pragmatic Clean Architecture + CQRS/MediatR, EF Core + Postgres. Reservation becomes its own aggregate (`src/Rpom.Application/Reservation/<UseCase>/`). Booked tables modelled as a `ReservationTable` junction (many-to-many); seated tickets linked by a nullable `Ticket.ReservationId` FK (one-to-many). Hold/overlap math lives in a pure `ReservationWindow` helper. Concurrency follows the existing codebase: `ITableOperationGuard` heartbeat lock for table operations (as in `OpenTicket`), optimistic `Version` token on the reservation row, and the existing `TransactionPipelineBehavior` that wraps each command in one transaction (no raw `SELECT FOR UPDATE` — the codebase does not use it).

**Tech Stack:** ASP.NET Core 10, EF Core (Npgsql, snake_case), MediatR, FluentValidation, xUnit + FluentAssertions + NSubstitute + Testcontainers (pgvector/pgvector:pg17).

**Concurrency note (reconciliation with spec §7):** The spec says "pessimistic lock (SELECT FOR UPDATE)". The codebase has no raw row-lock usage; it relies on (a) the per-table heartbeat lock via `ITableOperationGuard.EnsureHeldAsync`, (b) optimistic `Version` (`IsConcurrencyToken`) on hub rows, and (c) `TransactionPipelineBehavior` wrapping each `IBaseCommand` atomically. This plan uses (a)+(b)+(c), which achieves the same race protection (concurrent seat/cancel/expire) while staying idiomatic.

---

## File Structure

**Domain (`src/Rpom.Domain/`)**
- Modify `Reservation/Reservation.cs` — drop `TableId`/`LinkedTicketId` + their navs; add `CounterId`, `Version`, `ReservationTables` collection nav.
- Modify `Reservation/ReservationStatus.cs` — add `NotArrived = "NOT_ARRIVED"`.
- Create `Reservation/ReservationTable.cs` — junction entity.
- Create `Reservation/ReservationErrors.cs` — aggregate errors.
- Modify `Sales/Ticket.cs` — add `ReservationId` nullable + nav.

**Application (`src/Rpom.Application/`)**
- Create `Reservation/ReservationWindow.cs` — pure hold-window/overlap/phase helper.
- Create `Reservation/CreateReservation/CreateReservation.cs`.
- Create `Reservation/GetReservationList/GetReservationList.cs` (lazy-expire on read).
- Create `Reservation/GetReservationFloorPlanProjection/GetReservationFloorPlanProjection.cs` (UC-R5).
- Create `Reservation/SeatReservation/SeatReservation.cs`.
- Create `Reservation/CancelReservation/CancelReservation.cs`.
- Modify `Access/Permissions.cs` — add `ReservationView`, `ReservationSeat`.
- Modify `Access/RolePermissionDefaults.cs` — grant the 4 reservation perms to Cashier; add view+seat to OrderStaff & Manager.

**Infrastructure (`src/Rpom.Infrastructure/`)**
- Modify `Database/Configurations/Reservation/ReservationConfiguration.cs`.
- Create `Database/Configurations/Reservation/ReservationTableConfiguration.cs`.
- Modify `Database/Configurations/Sales/TicketConfiguration.cs` — add `ReservationId` FK.
- Modify `Database/ApplicationDbContext.cs` + `Application/Abstraction/Data/IDbContext.cs` — add `DbSet<ReservationTable>`.
- Modify `Database/Seeding/AccessSeeder.cs` — catalog rows for the 2 new perms.
- Modify `Database/Seeding/CashierDemoSeeder.cs` — grant view+seat to the demo cashier.
- New migration `AddReservationRedesign`.

**Api (`src/Rpom.Api/Endpoints/Cashier/Reservations/`)** — one endpoint file per use case, tag `"Reservations"`, route `api/reservations/...`.
- `CreateReservationEndpoint.cs`, `GetReservationListEndpoint.cs`, `GetReservationFloorPlanProjectionEndpoint.cs`, `SeatReservationEndpoint.cs`, `CancelReservationEndpoint.cs`.

**Modify existing read API**
- `src/Rpom.Application/Cashier/GetFloorPlan/GetFloorPlan.cs` — reservations now joined via `ReservationTable` (the `r.TableId` reference is being removed).

**Tests (`tests/Rpom.Application.Tests/Reservation/`)**
- `ReservationWindowTests.cs` (pure unit).
- `CreateReservationTests.cs`, `GetReservationListTests.cs`, `ReservationFloorPlanProjectionTests.cs`, `SeatReservationTests.cs`, `CancelReservationTests.cs` (Testcontainers integration).

> **Route decision:** endpoints use `api/reservations/...` (not `api/cashier/...`) because the feature is shared by Cashier and Order Staff. If the team prefers persona-prefixed routes, change the route strings — nothing else depends on them.

---

## Task 1: Domain restructure

**Files:**
- Modify: `src/Rpom.Domain/Reservation/Reservation.cs`
- Modify: `src/Rpom.Domain/Reservation/ReservationStatus.cs`
- Create: `src/Rpom.Domain/Reservation/ReservationTable.cs`
- Create: `src/Rpom.Domain/Reservation/ReservationErrors.cs`
- Modify: `src/Rpom.Domain/Sales/Ticket.cs`

- [ ] **Step 1: Add `NotArrived` to `ReservationStatus`**

In `src/Rpom.Domain/Reservation/ReservationStatus.cs`, inside the class body add:

```csharp
    public const string Booked = "BOOKED";
    public const string Arrived = "ARRIVED";
    public const string Cancelled = "CANCELLED";

    /// <summary>Past window_end while still BOOKED; set lazily on read of the list.</summary>
    public const string NotArrived = "NOT_ARRIVED";
```

(Replace the existing three-const block with the four-const block above.)

- [ ] **Step 2: Restructure `Reservation` entity**

Replace the body of `src/Rpom.Domain/Reservation/Reservation.cs` from the `public class Reservation` line down. Remove `TableId`, `LinkedTicketId`, and the `Table`/`LinkedTicket` navs. Add `CounterId`, `Version`, and the `ReservationTables` collection. Keep `using Rpom.Domain.Sales;` (for `CancellationReason`). Final file:

```csharp
using System.Collections.ObjectModel;
using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Domain.Reservation;

/// <summary>
///     Phone-booking record. Customer is anonymous (no Customer master — Glossary §9).
///     Books one or more tables (<see cref="ReservationTables" />), all in one counter.
///     Hold is derived at read time: a table is held iff a BOOKED reservation covers it and
///     now ∈ [TargetTime − pre_buffer, TargetTime + grace_period]. No placeholder ticket.
/// </summary>
public class Reservation : Entity
{
    public long Id { get; set; }

    /// <summary>Auto-generated business code, e.g. "R-2026-123".</summary>
    public string Code { get; set; } = null!;

    /// <summary>Denormalized owning counter — all booked tables share it. Mirrors Ticket.CounterId.</summary>
    public int CounterId { get; set; }

    public string CustomerName { get; set; } = null!;
    public string CustomerPhone { get; set; } = null!;
    public short GuestCount { get; set; } = 1;
    public string? Note { get; set; }

    /// <summary>When the customer is expected. Drives the hold window.</summary>
    public DateTime TargetTime { get; set; }

    /// <summary>BOOKED | ARRIVED | CANCELLED | NOT_ARRIVED (see <see cref="ReservationStatus" />).</summary>
    public string Status { get; set; } = ReservationStatus.Booked;

    public DateTime? ArrivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancellationReasonId { get; set; }
    public string? CancellationNote { get; set; }

    /// <summary>Staff who took the booking call.</summary>
    public int CreatedByStaffId { get; set; }

    /// <summary>Optimistic concurrency token (Cancel/Seat races). EF IsConcurrencyToken.</summary>
    public int Version { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — reservation list + floor-plan projection refresh.</summary>
    public DateTime UpdatedAt { get; set; }

    public virtual Counter Counter { get; set; } = null!;
    public virtual CancellationReason? CancellationReason { get; set; }
    public virtual StaffAccount CreatedByStaff { get; set; } = null!;

    /// <summary>The tables this reservation books (booking intent — overlap/projection/hold).</summary>
    public virtual ICollection<ReservationTable> ReservationTables { get; set; } =
        new Collection<ReservationTable>();
}
```

- [ ] **Step 3: Create `ReservationTable` junction entity**

Create `src/Rpom.Domain/Reservation/ReservationTable.cs`:

```csharp
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Reservation;

/// <summary>
///     Junction: one booked table of a reservation (many-to-many). Source of truth for
///     BR-R1 overlap, UC-R5 projection, and the floor-plan "đã đặt" badge. PK = (ReservationId, TableId).
/// </summary>
public class ReservationTable : Entity
{
    public long ReservationId { get; set; }
    public int TableId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Reservation Reservation { get; set; } = null!;
    public virtual Table Table { get; set; } = null!;
}
```

- [ ] **Step 4: Create `ReservationErrors`**

Create `src/Rpom.Domain/Reservation/ReservationErrors.cs`:

```csharp
using Rpom.Domain.Common;

namespace Rpom.Domain.Reservation;

public static class ReservationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Reservation.NotFound", "Đặt bàn không tồn tại.");

    public static readonly Error NotBooked = Error.Conflict(
        "Reservation.NotBooked", "Đặt bàn không ở trạng thái BOOKED.");

    public static readonly Error NoTables = Error.Validation(
        "Reservation.NoTables", "Phải chọn ít nhất một bàn.");

    public static readonly Error TablesCrossCounter = Error.Conflict(
        "Reservation.TablesCrossCounter", "Mọi bàn trong một đặt bàn phải thuộc cùng một quầy.");

    public static readonly Error TableOverlap = Error.Conflict(
        "Reservation.TableOverlap", "Bàn đã có đặt bàn khác trùng khung giờ giữ bàn.");

    public static readonly Error WindowExpired = Error.Conflict(
        "Reservation.WindowExpired", "Đã quá khung giờ giữ bàn — xử lý như khách vãng lai.");

    public static readonly Error SeatTablesCrossCounter = Error.Conflict(
        "Reservation.SeatTablesCrossCounter", "Bàn được chọn không thuộc quầy của đặt bàn.");
}
```

> Verify `Error.Validation`/`Error.NotFound`/`Error.Conflict` factory names exist in `src/Rpom.Domain/Common/Error.cs`; mirror whatever `TicketErrors.cs` uses (it uses `Error.NotFound`/`Error.Conflict`).

- [ ] **Step 5: Add `ReservationId` FK to `Ticket`**

In `src/Rpom.Domain/Sales/Ticket.cs`, add a nullable property near the other FK columns (and a nav if the file uses navs):

```csharp
    /// <summary>Set when this ticket was opened by seating a reservation (UC-R3). NULL for walk-ins.</summary>
    public long? ReservationId { get; set; }
```

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: FAILS — `ReservationConfiguration.cs` and `GetFloorPlan.cs` still reference removed `TableId`/`LinkedTicketId`. This is expected; Tasks 2 and 9 fix them. (If you want a green build first, do Task 2 before building.)

- [ ] **Step 7: Commit**

```bash
git add src/Rpom.Domain/Reservation src/Rpom.Domain/Sales/Ticket.cs
git commit -m "feat(reservation): restructure domain for multi-table bookings"
```

---

## Task 2: EF configuration + DbContext + migration

**Files:**
- Modify: `src/Rpom.Infrastructure/Database/Configurations/Reservation/ReservationConfiguration.cs`
- Create: `src/Rpom.Infrastructure/Database/Configurations/Reservation/ReservationTableConfiguration.cs`
- Modify: `src/Rpom.Infrastructure/Database/Configurations/Sales/TicketConfiguration.cs`
- Modify: `src/Rpom.Application/Abstraction/Data/IDbContext.cs`
- Modify: `src/Rpom.Infrastructure/Database/ApplicationDbContext.cs`

- [ ] **Step 1: Rewrite `ReservationConfiguration`**

Replace `src/Rpom.Infrastructure/Database/Configurations/Reservation/ReservationConfiguration.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Reservation;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Infrastructure.Database.Configurations.Reservation;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<ReservationEntity>
{
    public void Configure(EntityTypeBuilder<ReservationEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(30);
        builder.Property(x => x.CustomerName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CustomerPhone).IsRequired().HasMaxLength(20);
        builder.Property(x => x.GuestCount).HasDefaultValue((short)1);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20)
            .HasDefaultValue(ReservationStatus.Booked);
        builder.Property(x => x.CancellationNote).HasMaxLength(500);
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_reservation_status",
            "status IN ('BOOKED', 'ARRIVED', 'CANCELLED', 'NOT_ARRIVED')"));

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.CounterId, x.Status, x.TargetTime })
            .HasDatabaseName("ix_reservation_counter_status_target_time");
        builder.HasIndex(x => x.UpdatedAt).HasDatabaseName("ix_reservation_updated_at");
        builder.HasIndex(x => x.CreatedByStaffId);
        builder.HasIndex(x => x.CustomerPhone).HasDatabaseName("ix_reservation_phone");

        builder.HasOne(x => x.Counter)
            .WithMany()
            .HasForeignKey(x => x.CounterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CancellationReason)
            .WithMany()
            .HasForeignKey(x => x.CancellationReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByStaff)
            .WithMany()
            .HasForeignKey(x => x.CreatedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ReservationTables)
            .WithOne(rt => rt.Reservation)
            .HasForeignKey(rt => rt.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 2: Create `ReservationTableConfiguration`**

Create `src/Rpom.Infrastructure/Database/Configurations/Reservation/ReservationTableConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Reservation;

namespace Rpom.Infrastructure.Database.Configurations.Reservation;

internal sealed class ReservationTableConfiguration : IEntityTypeConfiguration<ReservationTable>
{
    public void Configure(EntityTypeBuilder<ReservationTable> builder)
    {
        builder.HasKey(x => new { x.ReservationId, x.TableId });

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.TableId)
            .HasDatabaseName("ix_reservation_table_table_id");

        builder.HasOne(x => x.Table)
            .WithMany()
            .HasForeignKey(x => x.TableId)
            .OnDelete(DeleteBehavior.Restrict);
        // Reservation side configured via ReservationConfiguration.HasMany(...).
    }
}
```

- [ ] **Step 3: Add `ReservationId` FK in `TicketConfiguration`**

In `src/Rpom.Infrastructure/Database/Configurations/Sales/TicketConfiguration.cs`, add (place near other `HasOne` FK mappings; use the navigation only if `Ticket` has one — Step 5 of Task 1 added just the scalar, so map without a nav):

```csharp
        builder.HasIndex(x => x.ReservationId).HasDatabaseName("ix_ticket_reservation_id");

        builder.HasOne<Rpom.Domain.Reservation.Reservation>()
            .WithMany()
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 4: Register `DbSet<ReservationTable>`**

In `src/Rpom.Application/Abstraction/Data/IDbContext.cs`, next to `DbSet<Reservation> Reservations { get; }` add:

```csharp
    DbSet<ReservationTable> ReservationTables { get; }
```

In `src/Rpom.Infrastructure/Database/ApplicationDbContext.cs`, add the matching property (mirror how `Reservations` is declared there, e.g. `public DbSet<ReservationTable> ReservationTables => Set<ReservationTable>();`).

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: FAILS only in `GetFloorPlan.cs` (still uses `r.TableId`). EF config + domain now compile. (Task 9 fixes `GetFloorPlan`; if you prefer green now, apply Task 9 Step 1 before building.)

- [ ] **Step 6: Generate the migration**

Run:
```bash
dotnet ef migrations add AddReservationRedesign --project src/Rpom.Infrastructure --startup-project src/Rpom.Api
```
Expected: a migration under `src/Rpom.Infrastructure/Database/Migrations/`.

- [ ] **Step 7: Review the migration up/down**

Open the generated `*_AddReservationRedesign.cs`. Confirm it:
- drops columns `table_id`, `linked_ticket_id` (and their FKs/indexes) from `reservations`;
- adds `counter_id` (NOT NULL, FK → counters), `version` (NOT NULL default 0) to `reservations`;
- replaces the old check constraint with `status IN ('BOOKED','ARRIVED','CANCELLED','NOT_ARRIVED')`;
- creates table `reservation_tables` (composite PK, FKs to reservations cascade + tables restrict);
- adds `reservation_id` (nullable, FK → reservations) + index to `tickets`.

The `reservations` table is empty in all environments (feature unbuilt), so the destructive column drops are safe. If your local DB has stray rows, truncate `reservations` first.

- [ ] **Step 8: Apply + smoke test locally**

Run:
```bash
dotnet ef database update --project src/Rpom.Infrastructure --startup-project src/Rpom.Api
```
Expected: applies cleanly.

- [ ] **Step 9: Commit**

```bash
git add src/Rpom.Infrastructure/Database src/Rpom.Application/Abstraction/Data/IDbContext.cs
git commit -m "feat(reservation): EF config, DbSet, and migration for redesign"
```

---

## Task 3: ReservationWindow helper (pure, TDD)

**Files:**
- Create: `src/Rpom.Application/Reservation/ReservationWindow.cs`
- Test: `tests/Rpom.Application.Tests/Reservation/ReservationWindowTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Rpom.Application.Tests/Reservation/ReservationWindowTests.cs`:

```csharp
using FluentAssertions;
using Rpom.Application.Reservation;

namespace Rpom.Application.Tests.Reservation;

public sealed class ReservationWindowTests
{
    private static readonly DateTime T18 = new(2026, 6, 27, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsHeld_InsideWindow_True()
    {
        ReservationWindow.IsHeld(T18, 30, 30, T18.AddMinutes(-10)).Should().BeTrue();
        ReservationWindow.IsHeld(T18, 30, 30, T18.AddMinutes(20)).Should().BeTrue();
    }

    [Fact]
    public void IsHeld_OutsideWindow_False()
    {
        ReservationWindow.IsHeld(T18, 30, 30, T18.AddMinutes(-31)).Should().BeFalse();
        ReservationWindow.IsHeld(T18, 30, 30, T18.AddMinutes(31)).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_IntervalNotPoint()
    {
        // existing 18:00 window [17:30,18:30]; new 18:45 window [18:15,19:15] → overlap at [18:15,18:30]
        ReservationWindow.Overlaps(T18, T18.AddMinutes(45), 30, 30).Should().BeTrue();
        // new 19:01 window [18:31,19:31] does NOT touch [17:30,18:30]
        ReservationWindow.Overlaps(T18, T18.AddMinutes(61), 30, 30).Should().BeFalse();
    }

    [Fact]
    public void Phase_Transitions()
    {
        ReservationWindow.Phase(T18, 30, 30, T18.AddMinutes(-31)).Should().Be("PENDING");
        ReservationWindow.Phase(T18, 30, 30, T18.AddMinutes(0)).Should().Be("HOLDING");
        ReservationWindow.Phase(T18, 30, 30, T18.AddMinutes(31)).Should().Be("EXPIRED");
    }
}
```

- [ ] **Step 2: Run — verify it fails to compile/fail**

Run: `dotnet test tests/Rpom.Application.Tests --filter ReservationWindowTests`
Expected: FAIL — `ReservationWindow` does not exist.

- [ ] **Step 3: Implement the helper**

Create `src/Rpom.Application/Reservation/ReservationWindow.cs`:

```csharp
namespace Rpom.Application.Reservation;

/// <summary>
///     Pure hold-window math. window = [TargetTime − preBuffer, TargetTime + grace].
///     Used by Create (overlap), List (phase + lazy-expire), FloorPlan + Projection (held).
/// </summary>
public static class ReservationWindow
{
    public static (DateTime Start, DateTime End) Compute(DateTime target, int preBufferMinutes, int graceMinutes)
        => (target.AddMinutes(-preBufferMinutes), target.AddMinutes(graceMinutes));

    public static bool IsHeld(DateTime target, int preBufferMinutes, int graceMinutes, DateTime now)
    {
        (DateTime start, DateTime end) = Compute(target, preBufferMinutes, graceMinutes);
        return start <= now && now <= end;
    }

    /// <summary>True when the two target times' hold windows intersect (interval overlap, BR-R1).</summary>
    public static bool Overlaps(DateTime targetA, DateTime targetB, int preBufferMinutes, int graceMinutes)
    {
        (DateTime aStart, DateTime aEnd) = Compute(targetA, preBufferMinutes, graceMinutes);
        (DateTime bStart, DateTime bEnd) = Compute(targetB, preBufferMinutes, graceMinutes);
        return aStart <= bEnd && bStart <= aEnd;
    }

    /// <summary>Derived UI phase of a BOOKED reservation: PENDING | HOLDING | EXPIRED.</summary>
    public static string Phase(DateTime target, int preBufferMinutes, int graceMinutes, DateTime now)
    {
        (DateTime start, DateTime end) = Compute(target, preBufferMinutes, graceMinutes);
        if (now < start) return "PENDING";
        return now <= end ? "HOLDING" : "EXPIRED";
    }
}
```

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test tests/Rpom.Application.Tests --filter ReservationWindowTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Rpom.Application/Reservation/ReservationWindow.cs tests/Rpom.Application.Tests/Reservation/ReservationWindowTests.cs
git commit -m "feat(reservation): pure hold-window/overlap/phase helper"
```

---

## Task 4: Permissions (view + seat)

**Files:**
- Modify: `src/Rpom.Application/Access/Permissions.cs`
- Modify: `src/Rpom.Application/Access/RolePermissionDefaults.cs`
- Modify: `src/Rpom.Infrastructure/Database/Seeding/AccessSeeder.cs`
- Modify: `src/Rpom.Infrastructure/Database/Seeding/CashierDemoSeeder.cs`

- [ ] **Step 1: Add permission constants**

In `src/Rpom.Application/Access/Permissions.cs`, replace the two reservation lines with four:

```csharp
        public const string ReservationView = "reservation:view";
        public const string ReservationCreate = "reservation:create";
        public const string ReservationSeat = "reservation:seat";
        public const string ReservationCancel = "reservation:cancel";
```

(Match the file's existing indentation — the existing lines are indented 4 spaces under the class.)

- [ ] **Step 2: Add catalog rows in `AccessSeeder`**

In `src/Rpom.Infrastructure/Database/Seeding/AccessSeeder.cs`, replace the two reservation catalog rows with four:

```csharp
            (Permissions.ReservationView, "View reservation list", PermissionGroups.Pos),
            (Permissions.ReservationCreate, "Create reservation", PermissionGroups.Pos),
            (Permissions.ReservationSeat, "Seat a reservation (open tickets)", PermissionGroups.Pos),
            (Permissions.ReservationCancel, "Cancel reservation", PermissionGroups.Pos),
```

- [ ] **Step 3: Update `RolePermissionDefaults`**

In `src/Rpom.Application/Access/RolePermissionDefaults.cs`:
- In the `[Roles.Cashier]` array, add a line (reservation is now a first-class cashier feature):
```csharp
                Permissions.ReservationView, Permissions.ReservationCreate,
                Permissions.ReservationSeat, Permissions.ReservationCancel,
```
- In the `[Roles.OrderStaff]` array, replace the existing reservation line with:
```csharp
                Permissions.ReservationView, Permissions.ReservationCreate,
                Permissions.ReservationSeat, Permissions.ReservationCancel,
```
- In the `[Roles.Manager]` array, add the same four-permission line.

- [ ] **Step 4: Grant to the demo cashier**

In `src/Rpom.Infrastructure/Database/Seeding/CashierDemoSeeder.cs`, replace the two reservation lines (around the `Permissions.ReservationCreate, Permissions.ReservationCancel,` block) with:

```csharp
                Permissions.ReservationView,
                Permissions.ReservationCreate,
                Permissions.ReservationSeat,
                Permissions.ReservationCancel,
```

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: PASS (assuming Task 9 Step 1 applied, or `GetFloorPlan` not yet built — if it still fails, it is only `GetFloorPlan.cs`; proceed to Task 9 then return). Permissions code compiles.

- [ ] **Step 6: Commit**

```bash
git add src/Rpom.Application/Access src/Rpom.Infrastructure/Database/Seeding/AccessSeeder.cs src/Rpom.Infrastructure/Database/Seeding/CashierDemoSeeder.cs
git commit -m "feat(reservation): add view + seat permissions and role defaults"
```

---

## Task 5: CreateReservation use case (UC-R1)

**Files:**
- Create: `src/Rpom.Application/Reservation/CreateReservation/CreateReservation.cs`
- Create: `src/Rpom.Api/Endpoints/Cashier/Reservations/CreateReservationEndpoint.cs`
- Test: `tests/Rpom.Application.Tests/Reservation/CreateReservationTests.cs`

> **Do NOT acquire the table operation lock here.** Creating a reservation only records a future booking; it does not touch the table's current operation, so it must not block or be blocked by whoever is actively serving that table now. The table lock (`ITableOperationGuard`) is only for present-time operations that open/mutate a ticket — that is `SeatReservation` (Task 8), not Create or Cancel. BR-R1 overlap (below) is the only contention check Create needs, and it is enforced with a plain query, not a lock.

- [ ] **Step 1: Write failing tests (happy path + cross-counter + overlap)**

Create `tests/Rpom.Application.Tests/Reservation/CreateReservationTests.cs`. Use the seeding/harness shape from `tests/Rpom.Application.Tests/Cashier/TransferTableTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Application.Reservation.CreateReservation;
using Rpom.Domain.Access;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Reservation;

public sealed class CreateReservationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId, _counter1, _tableA, _tableB, _tableOtherCounter;
    private static readonly DateTime Target = new(2026, 6, 28, 18, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_db.GetConnectionString(),
                o => o.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default).UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        _ctx = new ApplicationDbContext(options);
        await _ctx.Database.MigrateAsync();
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task MultiTable_SameCounter_CreatesBookedWithTables()
    {
        var res = await Handler().Handle(
            new CreateReservation.Command(_counter1, Target, "Long", "0901", 6, "sinh nhật",
                new[] { _tableA, _tableB }), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var r = await _ctx.Reservations.Include(x => x.ReservationTables)
            .FirstAsync(x => x.Id == res.Value.ReservationId);
        r.Status.Should().Be(ReservationStatus.Booked);
        r.CounterId.Should().Be(_counter1);
        r.Code.Should().StartWith("R-2026-");
        r.ReservationTables.Select(t => t.TableId).Should().BeEquivalentTo(new[] { _tableA, _tableB });
    }

    [Fact]
    public async Task CrossCounter_Fails()
    {
        var res = await Handler().Handle(
            new CreateReservation.Command(_counter1, Target, "Long", "0901", 4, null,
                new[] { _tableA, _tableOtherCounter }), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.TablesCrossCounter");
    }

    [Fact]
    public async Task OverlappingWindowOnSameTable_Fails()
    {
        await Handler().Handle(new CreateReservation.Command(
            _counter1, Target, "A", "1", 2, null, new[] { _tableA }), CancellationToken.None);
        // 18:45 window overlaps 18:00 window on the same table
        var res = await Handler().Handle(new CreateReservation.Command(
            _counter1, Target.AddMinutes(45), "B", "2", 2, null, new[] { _tableA }), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.TableOverlap");
    }

    private CreateReservation.Handler Handler() => new(_ctx, Staff(), Clock(), Config(), Version());

    private static IVersionService Version() => Substitute.For<IVersionService>();
    private static IConfigValueService Config()
    {
        var c = Substitute.For<IConfigValueService>();
        c.GetIntAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<int>(1)); // return the supplied default (30/30)
        return c;
    }
    private ICurrentStaff Staff()
    {
        var s = Substitute.For<ICurrentStaff>();
        s.StaffAccountId.Returns(_staffId);
        return s;
    }
    private static IDateTimeProvider Clock()
    {
        var c = Substitute.For<IDateTimeProvider>();
        c.UtcNow.Returns(_ => DateTime.UtcNow);
        return c;
    }

    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount { Username = "c", PasswordHash = "x", FullName = "Thu ngân", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var c1 = new Counter { Name = "C1", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var c2 = new Counter { Name = "C2", DisplayOrder = 2, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var a1 = new Area { Counter = c1, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var a2 = new Area { Counter = c2, Name = "B", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var tA = new Table { Area = a1, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tB = new Table { Area = a1, Code = "T02", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tOther = new Table { Area = a2, Code = "T20", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        _ctx.AddRange(role, staff, c1, c2, a1, a2, tA, tB, tOther);
        await _ctx.SaveChangesAsync();
        _staffId = staff.Id; _counter1 = c1.Id; _tableA = tA.Id; _tableB = tB.Id; _tableOtherCounter = tOther.Id;
    }
}
```

- [ ] **Step 2: Run — verify fail**

Run: `dotnet test tests/Rpom.Application.Tests --filter CreateReservationTests`
Expected: FAIL — `CreateReservation` does not exist.

- [ ] **Step 3: Implement the handler**

Create `src/Rpom.Application/Reservation/CreateReservation/CreateReservation.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Application.Reservation.CreateReservation;

/// <summary>
///     UC-R1. Books one or more tables (all in <paramref name="CounterId" />) for a future time.
///     Rejects cross-counter table sets (BR-R6) and per-table window overlaps (BR-R1).
/// </summary>
public static class CreateReservation
{
    public sealed record Command(
        int CounterId,
        DateTime TargetTime,
        string CustomerName,
        string CustomerPhone,
        short GuestCount,
        string? Note,
        IReadOnlyList<int> TableIds) : ICommand<Response>;

    public sealed record Response(long ReservationId, string Code);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CounterId).GreaterThan(0);
            RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.CustomerPhone).NotEmpty().MaximumLength(20);
            RuleFor(x => x.GuestCount).GreaterThan((short)0);
            RuleFor(x => x.TableIds).NotEmpty();
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IConfigValueService config,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var tableIds = request.TableIds.Distinct().ToList();
            if (tableIds.Count == 0)
            {
                return Result.Failure<Response>(ReservationErrors.NoTables);
            }

            var tables = await db.Tables
                .Where(t => tableIds.Contains(t.Id) && t.IsActive)
                .Select(t => new { t.Id, t.Area.CounterId })
                .ToListAsync(ct);
            if (tables.Count != tableIds.Count)
            {
                return Result.Failure<Response>(TableErrors.NotFound);
            }
            if (tables.Any(t => t.CounterId != request.CounterId))
            {
                return Result.Failure<Response>(ReservationErrors.TablesCrossCounter);
            }

            int pre = await config.GetIntAsync(ConfigCodes.ReservationPreBufferMinutes, 30, ct);
            int grace = await config.GetIntAsync(ConfigCodes.ReservationGracePeriodMinutes, 30, ct);

            // BR-R1: per-table interval overlap against existing BOOKED reservations.
            var existing = await db.ReservationTables
                .Where(rt => tableIds.Contains(rt.TableId)
                             && rt.Reservation.Status == ReservationStatus.Booked)
                .Select(rt => rt.Reservation.TargetTime)
                .ToListAsync(ct);
            if (existing.Any(t => ReservationWindow.Overlaps(t, request.TargetTime, pre, grace)))
            {
                return Result.Failure<Response>(ReservationErrors.TableOverlap);
            }

            DateTime now = clock.UtcNow;
            var reservation = new ReservationEntity
            {
                Code = "R-PENDING",
                CounterId = request.CounterId,
                CustomerName = request.CustomerName.Trim(),
                CustomerPhone = request.CustomerPhone.Trim(),
                GuestCount = request.GuestCount,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                TargetTime = request.TargetTime,
                Status = ReservationStatus.Booked,
                CreatedByStaffId = currentStaff.StaffAccountId,
                CreatedAt = now,
                UpdatedAt = now,
                ReservationTables = tableIds
                    .Select(id => new ReservationTable { TableId = id, CreatedAt = now })
                    .ToList()
            };
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);

            reservation.Code = $"R-{now:yyyy}-{reservation.Id}";

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ReservationEntity),
                EntityId = reservation.Id,
                Action = "CREATE",
                ActorStaffAccountId = staff.Id,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Reservation created: {reservation.Code} on {tableIds.Count} table(s) @ {request.TargetTime:u}"
            });
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Reservation.Create(id={reservation.Id})", ct);

            return Result.Success(new Response(reservation.Id, reservation.Code));
        }
    }
}
```

> Confirm `IConfigValueService.GetIntAsync(string, int, CancellationToken)` exists (used by `GetFloorPlan.cs:80`). It does.

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test tests/Rpom.Application.Tests --filter CreateReservationTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Create the endpoint**

Create `src/Rpom.Api/Endpoints/Cashier/Reservations/CreateReservationEndpoint.cs` (mirror `OpenTicketEndpoint.cs`):

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.CreateReservation;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class CreateReservationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/reservations",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateReservation.Response> result = await sender.Send(new CreateReservation.Command(
                        request.CounterId, request.TargetTime, request.CustomerName, request.CustomerPhone,
                        request.GuestCount, request.Note, request.TableIds), ct);
                    return result.MatchCreated(r => $"/api/reservations/{r.ReservationId}");
                })
            .RequireAuthorization(Permissions.ReservationCreate)
            .WithTags("Reservations")
            .WithName("CreateReservation")
            .Produces<ApiResult<CreateReservation.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a multi-table phone reservation.");
    }

    internal sealed record Request(
        int CounterId, DateTime TargetTime, string CustomerName, string CustomerPhone,
        short GuestCount, string? Note, IReadOnlyList<int> TableIds);
}
```

- [ ] **Step 6: Build + commit**

Run: `dotnet build`
Expected: PASS.

```bash
git add src/Rpom.Application/Reservation/CreateReservation src/Rpom.Api/Endpoints/Cashier/Reservations/CreateReservationEndpoint.cs tests/Rpom.Application.Tests/Reservation/CreateReservationTests.cs
git commit -m "feat(reservation): CreateReservation use case (UC-R1)"
```

---

## Task 6: GetReservationFloorPlanProjection (UC-R5)

**Files:**
- Create: `src/Rpom.Application/Reservation/GetReservationFloorPlanProjection/GetReservationFloorPlanProjection.cs`
- Create: `src/Rpom.Api/Endpoints/Cashier/Reservations/GetReservationFloorPlanProjectionEndpoint.cs`
- Test: `tests/Rpom.Application.Tests/Reservation/ReservationFloorPlanProjectionTests.cs`

- [ ] **Step 1: Write failing test**

Create `tests/Rpom.Application.Tests/Reservation/ReservationFloorPlanProjectionTests.cs`. Reuse the seed shape from Task 5 (copy `SeedAsync`, the container setup, `Config`, `Clock`, `Staff` helpers). Add a booked reservation on `_tableA` at `Target`, then assert the projection:

```csharp
    [Fact]
    public async Task Projection_FlagsOverlapTablesAndReturnsReservations()
    {
        // Seed one BOOKED reservation on tableA at 18:00.
        await new Rpom.Application.Reservation.CreateReservation.CreateReservation.Handler(
                _ctx, Staff(), Clock(), Config(), Substitute.For<IVersionService>())
            .Handle(new Rpom.Application.Reservation.CreateReservation.CreateReservation.Command(
                _counter1, Target, "Long", "0901", 4, null, new[] { _tableA }), CancellationToken.None);

        var res = await new GetReservationFloorPlanProjection.Handler(_ctx, Config())
            .Handle(new GetReservationFloorPlanProjection.Query(_counter1, Target.AddMinutes(45)),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var tA = res.Value.Tables.Single(t => t.TableId == _tableA);
        tA.IsReservedOverlap.Should().BeTrue();   // 18:45 window overlaps 18:00 window
        var tB = res.Value.Tables.Single(t => t.TableId == _tableB);
        tB.IsReservedOverlap.Should().BeFalse();
        res.Value.OverlappingReservations.Should().ContainSingle();
    }
```

- [ ] **Step 2: Run — verify fail**

Run: `dotnet test tests/Rpom.Application.Tests --filter ReservationFloorPlanProjectionTests`
Expected: FAIL — type does not exist.

- [ ] **Step 3: Implement the handler**

Create `src/Rpom.Application/Reservation/GetReservationFloorPlanProjection/GetReservationFloorPlanProjection.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Configuration;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Reservation.GetReservationFloorPlanProjection;

/// <summary>
///     UC-R5. Floor plan of a counter PROJECTED to a target time: each table is flagged
///     reserved-overlap iff a BOOKED reservation's window would overlap a new booking at
///     <paramref name="TargetTime" /> (BR-R1). Read-only.
/// </summary>
public static class GetReservationFloorPlanProjection
{
    public sealed record Query(int CounterId, DateTime TargetTime) : IQuery<Response>;

    public sealed record Response(
        int CounterId,
        DateTime TargetTime,
        IReadOnlyList<TableProjection> Tables,
        IReadOnlyList<ReservationBrief> OverlappingReservations);

    public sealed record TableProjection(
        int TableId, string TableCode, int AreaId, string AreaName, int SeatCount, bool IsReservedOverlap);

    public sealed record ReservationBrief(
        long ReservationId, string CustomerName, string CustomerPhone, short GuestCount,
        DateTime TargetTime, IReadOnlyList<int> TableIds);

    internal sealed class Handler(IDbContext db, IConfigValueService config)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            bool counterExists = await db.Counters.AnyAsync(c => c.Id == request.CounterId && c.IsActive, ct);
            if (!counterExists)
            {
                return Result.Failure<Response>(CounterErrors.NotFound);
            }

            int pre = await config.GetIntAsync(ConfigCodes.ReservationPreBufferMinutes, 30, ct);
            int grace = await config.GetIntAsync(ConfigCodes.ReservationGracePeriodMinutes, 30, ct);

            var tables = await db.Tables
                .Where(t => t.Area.CounterId == request.CounterId && t.IsActive)
                .OrderBy(t => t.AreaId).ThenBy(t => t.Code)
                .Select(t => new { t.Id, t.Code, t.AreaId, AreaName = t.Area.Name, t.SeatCount })
                .ToListAsync(ct);

            // All BOOKED reservations in this counter with their booked table ids.
            var booked = await db.Reservations
                .Where(r => r.CounterId == request.CounterId && r.Status == ReservationStatus.Booked)
                .Select(r => new
                {
                    r.Id, r.CustomerName, r.CustomerPhone, r.GuestCount, r.TargetTime,
                    TableIds = r.ReservationTables.Select(rt => rt.TableId).ToList()
                })
                .ToListAsync(ct);

            var overlapping = booked
                .Where(r => ReservationWindow.Overlaps(r.TargetTime, request.TargetTime, pre, grace))
                .ToList();
            var overlapTableIds = overlapping.SelectMany(r => r.TableIds).ToHashSet();

            var tableDtos = tables.Select(t => new TableProjection(
                t.Id, t.Code, t.AreaId, t.AreaName, t.SeatCount, overlapTableIds.Contains(t.Id))).ToList();

            var resBriefs = overlapping.Select(r => new ReservationBrief(
                r.Id, r.CustomerName, r.CustomerPhone, r.GuestCount, r.TargetTime, r.TableIds)).ToList();

            return Result.Success(new Response(request.CounterId, request.TargetTime, tableDtos, resBriefs));
        }
    }
}
```

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test tests/Rpom.Application.Tests --filter ReservationFloorPlanProjectionTests`
Expected: PASS.

- [ ] **Step 5: Create the endpoint**

Create `src/Rpom.Api/Endpoints/Cashier/Reservations/GetReservationFloorPlanProjectionEndpoint.cs`:

```csharp
using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.GetReservationFloorPlanProjection;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class GetReservationFloorPlanProjectionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reservations/projection",
                async (int counterId, DateTime targetTime, ISender sender, CancellationToken ct) =>
                {
                    Result<GetReservationFloorPlanProjection.Response> result =
                        await sender.Send(new GetReservationFloorPlanProjection.Query(counterId, targetTime), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReservationCreate)
            .WithTags("Reservations")
            .WithName("GetReservationFloorPlanProjection")
            .WithSummary("Floor plan of a counter projected to a target booking time (UC-R5).");
    }
}
```

> `MatchOk()` is the same extension `GetTicketDetailsEndpoint` uses; confirm in `src/Rpom.Api/Results/`.

- [ ] **Step 6: Build + commit**

Run: `dotnet build`
```bash
git add src/Rpom.Application/Reservation/GetReservationFloorPlanProjection src/Rpom.Api/Endpoints/Cashier/Reservations/GetReservationFloorPlanProjectionEndpoint.cs tests/Rpom.Application.Tests/Reservation/ReservationFloorPlanProjectionTests.cs
git commit -m "feat(reservation): floor-plan projection to booking time (UC-R5)"
```

---

## Task 7: GetReservationList with lazy-expire (UC-R2)

**Files:**
- Create: `src/Rpom.Application/Reservation/GetReservationList/GetReservationList.cs`
- Create: `src/Rpom.Api/Endpoints/Cashier/Reservations/GetReservationListEndpoint.cs`
- Test: `tests/Rpom.Application.Tests/Reservation/GetReservationListTests.cs`

> **Deliberate exception:** this is an `IQuery` that writes (lazy-expire, per spec D7/BR-R8). Queries are not wrapped by `TransactionPipelineBehavior`, but `SaveChangesAsync` batches the expiry atomically. Keep the write confined to this handler.

- [ ] **Step 1: Write failing tests (expired flips to NOT_ARRIVED; phases labelled)**

Create `tests/Rpom.Application.Tests/Reservation/GetReservationListTests.cs`. Reuse the Task 5 harness/seed. Key test: create a reservation whose window already ended (TargetTime far in the past), list it, assert it became `NOT_ARRIVED`.

```csharp
    [Fact]
    public async Task ListToday_ExpiredBooked_FlipsToNotArrived()
    {
        // Booked yesterday → window long past.
        var past = DateTime.UtcNow.AddDays(-1);
        await CreateAt(past);

        var res = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, DateOnly.FromDateTime(past), null),
                CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Items.Should().ContainSingle()
            .Which.Status.Should().Be(ReservationStatus.NotArrived);
        (await _ctx.Reservations.AsNoTracking().FirstAsync()).Status
            .Should().Be(ReservationStatus.NotArrived);
    }

    [Fact]
    public async Task ListToday_HoldingBooked_LabelledHolding()
    {
        await CreateAt(DateTime.UtcNow); // now ∈ window
        var res = await new GetReservationList.Handler(_ctx, Staff(), Clock(), Config())
            .Handle(new GetReservationList.Query(_counter1, DateOnly.FromDateTime(DateTime.UtcNow), null),
                CancellationToken.None);
        res.Value.Items.Single().Phase.Should().Be("HOLDING");
        res.Value.Items.Single().Status.Should().Be(ReservationStatus.Booked);
    }

    private async Task CreateAt(DateTime target) =>
        await new Rpom.Application.Reservation.CreateReservation.CreateReservation.Handler(
                _ctx, Staff(), Clock(), Config(), Substitute.For<IVersionService>())
            .Handle(new Rpom.Application.Reservation.CreateReservation.CreateReservation.Command(
                _counter1, target, "Long", "0901", 4, null, new[] { _tableA }), CancellationToken.None);
```

- [ ] **Step 2: Run — verify fail**

Run: `dotnet test tests/Rpom.Application.Tests --filter GetReservationListTests`
Expected: FAIL — type does not exist.

- [ ] **Step 3: Implement the handler**

Create `src/Rpom.Application/Reservation/GetReservationList/GetReservationList.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Configuration;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Application.Reservation.GetReservationList;

/// <summary>
///     UC-R2. Counter-scoped, day-filtered, time-sorted list. Performs lazy-expire (BR-R8):
///     BOOKED reservations past window_end are flipped to NOT_ARRIVED on read.
/// </summary>
public static class GetReservationList
{
    public sealed record Query(int CounterId, DateOnly Date, string? Status) : IQuery<Response>;

    public sealed record Response(IReadOnlyList<Item> Items);

    public sealed record Item(
        long ReservationId, string Code, string CustomerName, string CustomerPhone,
        short GuestCount, DateTime TargetTime, string Status, string? Phase,
        IReadOnlyList<int> TableIds);

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IConfigValueService config) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            DateTime now = clock.UtcNow;
            int pre = await config.GetIntAsync(ConfigCodes.ReservationPreBufferMinutes, 30, ct);
            int grace = await config.GetIntAsync(ConfigCodes.ReservationGracePeriodMinutes, 30, ct);

            DateTime dayStart = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            DateTime dayEnd = dayStart.AddDays(1);

            // Tracked load so we can flip expired rows in place.
            List<ReservationEntity> rows = await db.Reservations
                .Where(r => r.CounterId == request.CounterId
                            && r.TargetTime >= dayStart && r.TargetTime < dayEnd)
                .OrderBy(r => r.TargetTime)
                .ToListAsync(ct);

            // BR-R8 lazy-expire.
            var expired = rows
                .Where(r => r.Status == ReservationStatus.Booked
                            && now > r.TargetTime.AddMinutes(grace))
                .ToList();
            if (expired.Count > 0)
            {
                StaffAccount actor = await db.StaffAccounts
                    .FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
                foreach (ReservationEntity r in expired)
                {
                    r.Status = ReservationStatus.NotArrived;
                    r.UpdatedAt = now;
                    db.AuditLogs.Add(new AuditLog
                    {
                        EntityType = nameof(ReservationEntity),
                        EntityId = r.Id,
                        Action = "NOT_ARRIVED",
                        ActorStaffAccountId = actor.Id,
                        ActorFullName = actor.FullName,
                        Timestamp = now,
                        Summary = $"Reservation {r.Code} auto-expired (no-show) past window."
                    });
                }
                await db.SaveChangesAsync(ct);
            }

            var statusFilter = request.Status;
            var tableIdsByRes = await db.ReservationTables
                .Where(rt => rows.Select(r => r.Id).Contains(rt.ReservationId))
                .GroupBy(rt => rt.ReservationId)
                .Select(g => new { ReservationId = g.Key, Ids = g.Select(x => x.TableId).ToList() })
                .ToListAsync(ct);
            var idsMap = tableIdsByRes.ToDictionary(x => x.ReservationId, x => x.Ids);

            var items = rows
                .Where(r => statusFilter is null || r.Status == statusFilter)
                .Select(r => new Item(
                    r.Id, r.Code, r.CustomerName, r.CustomerPhone, r.GuestCount, r.TargetTime,
                    r.Status,
                    r.Status == ReservationStatus.Booked
                        ? ReservationWindow.Phase(r.TargetTime, pre, grace, now)
                        : null,
                    idsMap.GetValueOrDefault(r.Id) ?? new List<int>()))
                .ToList();

            return Result.Success(new Response(items));
        }
    }
}
```

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test tests/Rpom.Application.Tests --filter GetReservationListTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Create the endpoint**

Create `src/Rpom.Api/Endpoints/Cashier/Reservations/GetReservationListEndpoint.cs`:

```csharp
using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.GetReservationList;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class GetReservationListEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reservations",
                async (int counterId, DateOnly date, string? status, ISender sender, CancellationToken ct) =>
                {
                    Result<GetReservationList.Response> result =
                        await sender.Send(new GetReservationList.Query(counterId, date, status), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReservationView)
            .WithTags("Reservations")
            .WithName("GetReservationList")
            .WithSummary("Counter-scoped, day-filtered reservation list (lazy-expires no-shows).");
    }
}
```

- [ ] **Step 6: Build + commit**

```bash
git add src/Rpom.Application/Reservation/GetReservationList src/Rpom.Api/Endpoints/Cashier/Reservations/GetReservationListEndpoint.cs tests/Rpom.Application.Tests/Reservation/GetReservationListTests.cs
git commit -m "feat(reservation): reservation list with lazy no-show expiry (UC-R2)"
```

---

## Task 8: SeatReservation (UC-R3)

**Files:**
- Create: `src/Rpom.Application/Reservation/SeatReservation/SeatReservation.cs`
- Create: `src/Rpom.Api/Endpoints/Cashier/Reservations/SeatReservationEndpoint.cs`
- Test: `tests/Rpom.Application.Tests/Reservation/SeatReservationTests.cs`

> Mirrors `OpenTicket` for the ticket-creation part (lookup table area/SC + open drawer, create ticket, mark table occupied), looping over the actually-selected tables and additionally setting `Ticket.ReservationId`. Requires each selected table's operation lock (`ITableOperationGuard.EnsureHeldAsync`), exactly like `OpenTicket`. Re-checks BR-R7 (`BOOKED` + `now ≤ window_end`) before opening.
>
> **Multi-table locking:** locks are per-table (one `TableLock` row each); a single staff may hold several simultaneously. The FE acquires the operation lock on **every selected table** before calling seat (same as opening a ticket). The handler then verifies each via `EnsureHeldAsync` in a loop — if **any** selected table is not held (held by someone else / lock lapsed), the whole seat fails and the surrounding transaction rolls back, so no partial tickets are opened. These are heartbeat locks (not `SELECT FOR UPDATE`), so contending seats over overlapping table sets cannot deadlock — the loser simply gets a `TableLock.NotHeld` error and retries.

- [ ] **Step 1: Write failing tests (happy multi-table; expired-window reject)**

Create `tests/Rpom.Application.Tests/Reservation/SeatReservationTests.cs`. Seed like Task 5 PLUS a `Shift` + OPEN `CashDrawerSession` on the counter (copy from `TransferTableTests.SeedAsync`), and acquire the table lock before seating (use `AcquireTableLock.Handler` like `TransferTableTests.OpenOnTableA`). Core tests:

```csharp
    [Fact]
    public async Task Seat_OpensOneTicketPerSelectedTable_AndMarksArrived()
    {
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA, _tableB });
        await Lock(_tableA); await Lock(_tableB);

        var res = await Seat().Handle(new SeatReservation.Command(rid, new[]
        {
            new SeatReservation.SeatTable(_tableA, 3),
            new SeatReservation.SeatTable(_tableB, 3)
        }), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Tickets.Should().HaveCount(2);
        (await _ctx.Reservations.AsNoTracking().FirstAsync(x => x.Id == rid)).Status
            .Should().Be(ReservationStatus.Arrived);
        (await _ctx.Tickets.CountAsync(t => t.ReservationId == rid)).Should().Be(2);
        (await _ctx.Tables.Where(t => t.Id == _tableA).Select(t => t.Status).FirstAsync())
            .Should().Be(TableStatus.Occupied);
    }

    [Fact]
    public async Task Seat_PastWindow_Fails()
    {
        long rid = await CreateBooking(DateTime.UtcNow.AddDays(-1), new[] { _tableA });
        await Lock(_tableA);
        var res = await Seat().Handle(new SeatReservation.Command(rid,
            new[] { new SeatReservation.SeatTable(_tableA, 2) }), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.WindowExpired");
    }
```

Add helpers `Seat()`, `Lock(tableId)`, `CreateBooking(target, tableIds)` mirroring the Task 5 helpers and `TransferTableTests` (`AcquireTableLock.Handler`, `TableOperationGuard`).

- [ ] **Step 2: Run — verify fail**

Run: `dotnet test tests/Rpom.Application.Tests --filter SeatReservationTests`
Expected: FAIL — type does not exist.

- [ ] **Step 3: Implement the handler**

Create `src/Rpom.Application/Reservation/SeatReservation/SeatReservation.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Application.Reservation.SeatReservation;

/// <summary>
///     UC-R3. Opens one independent ticket per selected table (actual seated tables, which may
///     differ from the booked tables), links them via Ticket.ReservationId, marks the reservation
///     ARRIVED. Requires each table's operation lock. Rejects past-window (BR-R7) and cross-counter.
/// </summary>
public static class SeatReservation
{
    public sealed record SeatTable(int TableId, short GuestCount);

    public sealed record Command(long ReservationId, IReadOnlyList<SeatTable> Tables) : ICommand<Response>;

    public sealed record Response(IReadOnlyList<OpenedTicket> Tickets);

    public sealed record OpenedTicket(long TicketId, string Code, int TableId);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ReservationId).GreaterThan(0L);
            RuleFor(x => x.Tables).NotEmpty();
            RuleForEach(x => x.Tables).ChildRules(t =>
            {
                t.RuleFor(y => y.TableId).GreaterThan(0);
                t.RuleFor(y => y.GuestCount).GreaterThan((short)0);
            });
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        ITableOperationGuard guard,
        IConfigValueService config,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            int staffId = currentStaff.StaffAccountId;
            var seatTables = request.Tables.GroupBy(t => t.TableId)
                .Select(g => new SeatTable(g.Key, g.First().GuestCount)).ToList();

            ReservationEntity? reservation = await db.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct);
            if (reservation is null)
            {
                return Result.Failure<Response>(ReservationErrors.NotFound);
            }
            if (reservation.Status != ReservationStatus.Booked)
            {
                return Result.Failure<Response>(ReservationErrors.NotBooked);
            }

            int grace = await config.GetIntAsync(ConfigCodes.ReservationGracePeriodMinutes, 30, ct);
            DateTime now = clock.UtcNow;
            if (now > reservation.TargetTime.AddMinutes(grace))
            {
                return Result.Failure<Response>(ReservationErrors.WindowExpired);
            }

            // Each selected table must be held by this staff (same contract as OpenTicket).
            foreach (SeatTable st in seatTables)
            {
                Result held = await guard.EnsureHeldAsync(st.TableId, staffId, ct);
                if (held.IsFailure)
                {
                    return Result.Failure<Response>(held.Error);
                }
            }

            var tables = await db.Tables
                .Where(t => seatTables.Select(s => s.TableId).Contains(t.Id) && t.IsActive)
                .Select(t => new
                {
                    t.Id, t.AreaId, t.Area.CounterId,
                    t.Area.ServiceChargePercent, t.Area.ServiceChargeVatPercent
                })
                .ToListAsync(ct);
            if (tables.Count != seatTables.Count)
            {
                return Result.Failure<Response>(TableErrors.NotFound);
            }
            if (tables.Any(t => t.CounterId != reservation.CounterId))
            {
                return Result.Failure<Response>(ReservationErrors.SeatTablesCrossCounter);
            }

            var drawer = await db.CashDrawerSessions
                .Where(d => d.CounterId == reservation.CounterId && d.Status == CashDrawerStatus.Open)
                .Select(d => new { d.Id, d.ShiftId })
                .FirstOrDefaultAsync(ct);
            if (drawer is null)
            {
                return Result.Failure<Response>(TicketErrors.NoOpenCashDrawer);
            }

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            var opened = new List<Ticket>();
            foreach (SeatTable st in seatTables)
            {
                var tbl = tables.First(t => t.Id == st.TableId);
                var ticket = new Ticket
                {
                    Code = "TK-PENDING",
                    TableId = tbl.Id,
                    AreaId = tbl.AreaId,
                    CounterId = tbl.CounterId,
                    CashDrawerSessionId = drawer.Id,
                    ShiftId = drawer.ShiftId,
                    GuestCount = st.GuestCount,
                    WaiterStaffId = staffId,
                    Status = TicketStatus.Open,
                    OpenedAt = now,
                    ServiceChargePercent = tbl.ServiceChargePercent,
                    ServiceChargeVatPercent = tbl.ServiceChargeVatPercent,
                    ReservationId = reservation.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Tickets.Add(ticket);
                opened.Add(ticket);
            }
            await db.SaveChangesAsync(ct);

            foreach (Ticket ticket in opened)
            {
                ticket.Code = $"TK-{now:yyyyMMdd}-{ticket.Id}";
                Table tableRow = await db.Tables.FirstAsync(t => t.Id == ticket.TableId, ct);
                tableRow.Status = TableStatus.Occupied;
                tableRow.UpdatedAt = now;
                db.AuditLogs.Add(new AuditLog
                {
                    EntityType = nameof(Ticket),
                    EntityId = ticket.Id,
                    Action = "OPEN",
                    ActorStaffAccountId = staffId,
                    ActorFullName = staff.FullName,
                    Timestamp = now,
                    Summary = $"Ticket {ticket.Code} opened by seating reservation {reservation.Code}"
                });
            }

            reservation.Status = ReservationStatus.Arrived;
            reservation.ArrivedAt = now;
            reservation.UpdatedAt = now;
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ReservationEntity),
                EntityId = reservation.Id,
                Action = "SEAT",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Reservation {reservation.Code} seated on {opened.Count} table(s)."
            });
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Reservation.Seat(id={reservation.Id})", ct);

            return Result.Success(new Response(
                opened.Select(t => new OpenedTicket(t.Id, t.Code, t.TableId)).ToList()));
        }
    }
}
```

> If `Ticket` has required fields not set here (e.g. `Notes`), compare with `OpenTicket.cs:84-101` and copy any missing required initializers. `Notes` is nullable there, so omitting it is fine.

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test tests/Rpom.Application.Tests --filter SeatReservationTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Create the endpoint**

Create `src/Rpom.Api/Endpoints/Cashier/Reservations/SeatReservationEndpoint.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.SeatReservation;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class SeatReservationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/reservations/{reservationId:long}/seat",
                async (long reservationId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var tables = request.Tables
                        .Select(t => new SeatReservation.SeatTable(t.TableId, t.GuestCount)).ToList();
                    Result<SeatReservation.Response> result =
                        await sender.Send(new SeatReservation.Command(reservationId, tables), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReservationSeat)
            .WithTags("Reservations")
            .WithName("SeatReservation")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Seat a reservation: open one ticket per selected table (UC-R3).");
    }

    internal sealed record Request(IReadOnlyList<SeatTableDto> Tables);
    internal sealed record SeatTableDto(int TableId, short GuestCount);
}
```

- [ ] **Step 6: Build + commit**

```bash
git add src/Rpom.Application/Reservation/SeatReservation src/Rpom.Api/Endpoints/Cashier/Reservations/SeatReservationEndpoint.cs tests/Rpom.Application.Tests/Reservation/SeatReservationTests.cs
git commit -m "feat(reservation): seat reservation opens per-table tickets (UC-R3)"
```

---

## Task 9: CancelReservation (UC-R4) + fix GetFloorPlan

**Files:**
- Modify: `src/Rpom.Application/Cashier/GetFloorPlan/GetFloorPlan.cs`
- Create: `src/Rpom.Application/Reservation/CancelReservation/CancelReservation.cs`
- Create: `src/Rpom.Api/Endpoints/Cashier/Reservations/CancelReservationEndpoint.cs`
- Test: `tests/Rpom.Application.Tests/Reservation/CancelReservationTests.cs`

- [ ] **Step 1: Fix `GetFloorPlan` to join via `ReservationTable`**

In `src/Rpom.Application/Cashier/GetFloorPlan/GetFloorPlan.cs`, replace the reservations query (currently `db.Reservations.Where(r => tableIds.Contains(r.TableId) ...)`, lines ~132-144) with a junction join that keeps the same in-memory shape (`r.TableId` used later at lines ~164-168):

```csharp
            // Upcoming reservations for these tables (booked tables via the junction).
            var reservations = await db.ReservationTables
                .Where(rt => tableIds.Contains(rt.TableId)
                             && rt.Reservation.Status == ReservationStatus.Booked)
                .Select(rt => new
                {
                    rt.Reservation.Id,
                    rt.TableId,
                    rt.Reservation.CustomerName,
                    rt.Reservation.CustomerPhone,
                    rt.Reservation.GuestCount,
                    rt.Reservation.TargetTime,
                    rt.Reservation.Status
                })
                .ToListAsync(ct);
```

Optionally simplify the inline window math at lines ~164-168 to use the shared helper:
```csharp
                        var upcoming = reservations
                            .Where(r => r.TableId == t.Id
                                        && ReservationWindow.IsHeld(r.TargetTime, preBuffer, grace, now))
                            .OrderBy(r => r.TargetTime)
                            .FirstOrDefault();
```
Add `using Rpom.Application.Reservation;` if you use the helper.

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: PASS — all earlier reference errors resolved.

- [ ] **Step 3: Write failing tests**

Create `tests/Rpom.Application.Tests/Reservation/CancelReservationTests.cs` (reuse Task 5 harness; seed a `CancellationReason`). Tests:

```csharp
    [Fact]
    public async Task Cancel_BookedReservation_SetsCancelled()
    {
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA });
        var res = await Cancel().Handle(
            new CancelReservation.Command(rid, _reasonId, "khách huỷ"), CancellationToken.None);
        res.IsSuccess.Should().BeTrue();
        var r = await _ctx.Reservations.AsNoTracking().FirstAsync(x => x.Id == rid);
        r.Status.Should().Be(ReservationStatus.Cancelled);
        r.CancellationReasonId.Should().Be(_reasonId);
        r.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_NonBooked_Fails()
    {
        long rid = await CreateBooking(DateTime.UtcNow, new[] { _tableA });
        var r = await _ctx.Reservations.FirstAsync(x => x.Id == rid);
        r.Status = ReservationStatus.Cancelled; await _ctx.SaveChangesAsync();
        var res = await Cancel().Handle(
            new CancelReservation.Command(rid, _reasonId, null), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Reservation.NotBooked");
    }
```

Seed a `CancellationReason` in `SeedAsync` and store `_reasonId`:
```csharp
        var reason = new CancellationReason { Code = "NO_SHOW", Name = "Không đến", IsActive = true, CreatedAt = now, UpdatedAt = now };
        _ctx.Add(reason); // before SaveChanges; then _reasonId = reason.Id;
```
(Confirm `CancellationReason`'s required fields against `src/Rpom.Domain/Sales/CancellationReason.cs`.)

- [ ] **Step 4: Run — verify fail**

Run: `dotnet test tests/Rpom.Application.Tests --filter CancelReservationTests`
Expected: FAIL — type does not exist.

- [ ] **Step 5: Implement the handler**

Create `src/Rpom.Application/Reservation/CancelReservation/CancelReservation.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using Rpom.Domain.Sales;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Application.Reservation.CancelReservation;

/// <summary>UC-R4. Cancels a still-BOOKED reservation (customer phoned to cancel). Reason required (BR-CR1).</summary>
public static class CancelReservation
{
    public sealed record Command(long ReservationId, int CancellationReasonId, string? Note) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ReservationId).GreaterThan(0L);
            RuleFor(x => x.CancellationReasonId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            ReservationEntity? r = await db.Reservations
                .FirstOrDefaultAsync(x => x.Id == request.ReservationId, ct);
            if (r is null)
            {
                return Result.Failure(ReservationErrors.NotFound);
            }
            if (r.Status != ReservationStatus.Booked)
            {
                return Result.Failure(ReservationErrors.NotBooked);
            }

            bool reasonExists = await db.CancellationReasons
                .AnyAsync(x => x.Id == request.CancellationReasonId && x.IsActive, ct);
            if (!reasonExists)
            {
                return Result.Failure(CancellationReasonErrors.NotFound);
            }

            DateTime now = clock.UtcNow;
            r.Status = ReservationStatus.Cancelled;
            r.CancelledAt = now;
            r.CancellationReasonId = request.CancellationReasonId;
            r.CancellationNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            r.UpdatedAt = now;

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ReservationEntity),
                EntityId = r.Id,
                Action = "CANCEL",
                ActorStaffAccountId = staff.Id,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Reservation {r.Code} cancelled."
            });
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Reservation.Cancel(id={r.Id})", ct);

            return Result.Success();
        }
    }
}
```

> Confirm `CancellationReasonErrors.NotFound` exists (`src/Rpom.Domain/Sales/CancellationReasonErrors.cs`); if the code differs, use the actual one. Confirm `ICommand` (no response) + `ICommandHandler<Command>` exist by checking another command that returns no value (e.g. `RemoveDiscount` / `ReleaseTableLock`).

- [ ] **Step 6: Run — verify pass**

Run: `dotnet test tests/Rpom.Application.Tests --filter CancelReservationTests`
Expected: PASS (2 tests).

- [ ] **Step 7: Create the endpoint**

Create `src/Rpom.Api/Endpoints/Cashier/Reservations/CancelReservationEndpoint.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.CancelReservation;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class CancelReservationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/reservations/{reservationId:long}/cancel",
                async (long reservationId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(
                        new CancelReservation.Command(reservationId, request.CancellationReasonId, request.Note), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.ReservationCancel)
            .WithTags("Reservations")
            .WithName("CancelReservation")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Cancel a BOOKED reservation with a reason (UC-R4).");
    }

    internal sealed record Request(int CancellationReasonId, string? Note);
}
```

> Confirm `MatchNoContent()` exists for non-generic `Result` in `src/Rpom.Api/Results/` (CLAUDE.md §4 lists it). If the project uses a different name for void results, mirror an existing no-content endpoint (e.g. `ReleaseTableLockEndpoint`).

- [ ] **Step 8: Build + full test run + commit**

Run: `dotnet build && dotnet test tests/Rpom.Application.Tests`
Expected: PASS (all reservation tests + existing suite green; in particular `GetFloorPlan`-related tests still pass after the junction change).

```bash
git add src/Rpom.Application/Cashier/GetFloorPlan/GetFloorPlan.cs src/Rpom.Application/Reservation/CancelReservation src/Rpom.Api/Endpoints/Cashier/Reservations/CancelReservationEndpoint.cs tests/Rpom.Application.Tests/Reservation/CancelReservationTests.cs
git commit -m "feat(reservation): cancel use case (UC-R4) + floor-plan junction join"
```

---

## Task 10: Docs + changelog reconciliation

**Files:**
- Modify: `CHANGES.md`
- (Doc reconciliation per spec §9 — Glossary/ERD/Flows updates are out of code scope; add a tracking note.)

- [ ] **Step 1: Add a CHANGES.md entry**

Append a dated entry summarizing: multi-table reservation (junction), `Ticket.ReservationId`, `NOT_ARRIVED` status with lazy expiry, non-blocking hold, counter scope, 4 permissions, 5 endpoints under `api/reservations`. Reference the spec path.

- [ ] **Step 2: Note canonical-doc reconciliation**

In CHANGES.md (or a TODO note), record that `RPOM_Glossary.md` §4.8/§6.7/§7.5, `RPOM_Business_Flows.md` F6, `RPOM_Features_and_Screens.md`, `RPOM_Requirements.md`, and the Logical ERD still describe the pre-redesign single-table model and should be reconciled to this spec.

- [ ] **Step 3: Commit**

```bash
git add CHANGES.md
git commit -m "docs(reservation): changelog + doc-reconciliation note for redesign"
```

---

## Self-Review notes (for the implementer)

- **Spec coverage:** UC-R1→Task 5; UC-R2→Task 7; UC-R3→Task 8; UC-R4→Task 9; UC-R5→Task 6; BR-R2 walk-in warning → already surfaced by `GetFloorPlan.UpcomingReservation` (Task 9 keeps it working; FE shows the modal — no backend change beyond the existing field). BR-R8 lazy-expire → Task 7. Permissions → Task 4. Entity/migration → Tasks 1-2.
- **Type consistency:** `ReservationStatus.NotArrived == "NOT_ARRIVED"` used identically in EF check constraint (Task 2), list flip (Task 7), and tests. `Ticket.ReservationId` defined in Task 1, mapped in Task 2, set in Task 8, read in Task 9. `ReservationWindow.Overlaps/IsHeld/Phase` signatures match every call site.
- **Verify-before-use:** several steps flag "confirm X exists" for `Result`/`Error` factory names, `MatchOk/MatchCreated/MatchNoContent`, `ICommand` (void), `IConfigValueService.GetIntAsync`, and `CancellationReason` required fields — check the cited files before writing, since this plan asserts their shapes from the patterns observed in `OpenTicket`, `GetFloorPlan`, and `TransferTableTests`.
- **BR-R2 selectability in UI (spec §6):** the projection (Task 6) returns `IsReservedOverlap` so the FE can render would-overlap tables as non-selectable; the hard guarantee is BR-R1 enforced server-side in Task 5.
