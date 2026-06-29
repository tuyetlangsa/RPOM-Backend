# Reservation Redesign — Design Spec

**Date:** 2026-06-27
**Status:** Approved (brainstorming) — pending implementation plan
**Scope:** Reservation feature (phone bookings) for NextCashier / NextOrder. Capstone, single-tenant, VND.
**Supersedes (where conflicting):** `RPOM_Glossary.md` §3.18/§4.8/§6.7/§7.5, `RPOM_Business_Flows.md` F6, `RPOM_Requirements.md` A.3, `RPOM_Features_and_Screens.md` Reservation, `RPOM_Logical_ERD` Reservation (v0.20). This spec is the source of truth for the redesign; the canonical docs should be reconciled to it afterwards.

---

## 1. Why this redesign

The reservation feature was specced (v0.20) but never implemented beyond a domain skeleton (`Reservation` entity, `ReservationStatus`, EF config, 2 permissions, 2 config codes). During brainstorming the owner refined the behaviour in ways that diverge from the original docs:

1. **Cashier is a first-class reservation creator** (not only Order Staff). Reservation is a **separate module** reached from a button on the Cashier UI.
2. **Hold is a non-blocking warning.** When `now ∈ window`, the floor plan shows the table as reserved, but staff may still open a walk-in ticket on it (warning only). Rationale: a seated party may be about to leave; rigidly blocking the table is unnecessary since a reservation only books a table + party size in advance.
3. **A reservation may book multiple tables** (large party split across tables). Seating opens one ticket per selected table.
4. **No-show is a stored status `NotArrived`**, set lazily on read (no cron). Once `NotArrived`, the reservation cannot be seated — the party is handled as an ordinary walk-in.
5. **Counter-scoped like tickets:** Counter → Area → Table → Reservation.

---

## 2. Decisions locked during brainstorming

| # | Decision |
|---|---|
| D1 | Reservation is a separate module; entry is a button on the Cashier main screen. Creator = Cashier (Order Staff also allowed). |
| D2 | Hold = **display + warning only** on the floor plan while `now ∈ window`; walk-in tickets on a reserved table are always allowed (BR-R2). |
| D3 | One reservation may book **multiple tables**. |
| D4 | Seat opens **multiple independent tickets** (one per selected table), paid separately; cashier can merge later via existing Merge Bills (UC-08). |
| D5 | At seat time the cashier sees the **realtime** floor plan with the reserved tables **auto-selected**, and may change the selection (add/remove tables not in the original booking). |
| D6 | Tickets opened at seat are linked to the reservation **by the actual seated tables**, not the originally booked tables. Booked tables are still stored for overlap/audit. |
| D7 | Past-window no-show → stored status `NotArrived`, set **lazily on read** of the reservation list (no Quartz cron). |
| D8 | Once `NotArrived` (or past `window_end`), **seat-from-reservation is disabled**; the party is handled as a normal walk-in. |
| D9 | `Cancel` is for the customer-phoned-cancel case (and any manual cancel of a still-`BOOKED` reservation); requires a CancellationReason (BR-CR1). |
| D10 | Reservation is **counter-scoped**; all tables in one reservation must belong to the same counter. |
| D11 | Reservation Config (`pre_buffer_minutes`, `grace_period_minutes`) is stored in the DB (existing config codes / config-value table); no new config table. |

---

## 3. Screen Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  CASHIER MAIN (Floor Plan realtime)                               │
│   [Bàn 01] [Bàn 02] [Bàn 03] ...      ┌──────────────────┐        │
│   table with reservation in window →   │  📅 ĐẶT BÀN       │ ◄ module entry
│   shaded + badge "đã đặt HH:MM"        └────────┬─────────┘        │
└────────────────────────────────────────────────┼─────────────────┘
                                                   ▼
┌─────────────────────────────────────────────────────────────────┐
│  SC-A · RESERVATION LIST  (scoped to current counter, time-sorted) │
│  Filter: [Today ▾] [Status: All ▾]                [+ New booking]  │
│  ─────────────────────────────────────────────────────────────    │
│  ● 18:30  Anh Long · 0901… · 6 guests · Bàn 03,04   [HOLDING] ◄ hi-lite
│  ○ 19:00  Chị Mai  · 0938… · 2 guests · Bàn 07      [BOOKED]       │
│  ✗ 12:00  Anh Hùng · …     · 4 guests · Bàn 02      [NotArrived]   │
│        (each row → Detail)                                          │
└───────────┬──────────────────────────────────┬───────────────────┘
            │ [+ New booking]                   │ (select a row)
            ▼                                   ▼
┌──────────────────────────────┐   ┌────────────────────────────────┐
│ SC-B · CREATE RESERVATION     │   │ SC-C · RESERVATION DETAIL        │
│ 1) Target time: [__:__] 📅    │   │  Anh Long · 0901… · 6 guests     │
│    (mandatory, picked FIRST)  │   │  Target 18:30 · Bàn 03,04        │
│ 2) Floor plan PROJECTED to    │   │  Note: "birthday"                │
│    that time, with the list   │   │  Status: HOLDING                 │
│    of reservations overlapping│   │  ───────────────────────         │
│    that window overlaid →     │   │  [ Seat ]   [ Cancel ]           │
│    would-overlap = ⚠ (BR-R1,  │   │  (Seat hidden if NotArrived /    │
│    not selectable)            │   │   now > window_end)              │
│    ☑Bàn03 ☑Bàn04 ☐Bàn05       │   └──────────────┬─────────────────┘
│ 3) Customer: name / phone /   │                  │ [Seat]
│    guest count / note         │                  ▼
│         [ Save → BOOKED ]     │   ┌────────────────────────────────┐
└──────────────────────────────┘   │ SC-D · SEAT (Floor Plan REALTIME)│
                                    │  Booked tables auto-ticked:      │
                                    │   ☑Bàn03 ☑Bàn04                  │
                                    │  Cashier may change:             │
                                    │   ☑Bàn03 ☐Bàn04 ☑Bàn06           │
                                    │     [ Confirm → open N tickets ] │
                                    │  → 1 independent Ticket per table│
                                    │  → reservation → ARRIVED         │
                                    │  → back to Cashier Main          │
                                    └────────────────────────────────┘

   Side branch — open a walk-in ticket on a reserved table:
   Cashier Main → tap Bàn 03 (reservation in window) →
   ┌──────────────────────────────────────────┐
   │ ⚠ This table has a booking at 18:30        │
   │   [ Open walk-in anyway ]  [ Cancel ]      │
   └──────────────────────────────────────────┘   (allowed — BR-R2)
```

Notes:
- **SC-B floor plan = "projected to target time"** (state of each table *at the booking time*). **SC-D floor plan = "realtime now"**. Two different contexts; do not conflate.
- Reservation List default filter = "Today"; `NotArrived` / `CANCELLED` rows render dimmed, no Seat button.
- The "đã đặt" badge on Cashier Main appears only while a reservation is HOLDING (inside its window) — that is its whole purpose: tell staff a table is booked right now.

---

## 4. Use Cases

| UC | Name | Actor | Permission | Summary |
|---|---|---|---|---|
| **UC-R1** | Create Reservation | Cashier / Order Staff | `reservation:create` | Pick target time (mandatory) → projected floor plan of the counter + overlay of overlapping reservations → tick multiple tables (same counter) → enter customer info → save as `BOOKED` + N `ReservationTable` rows. |
| **UC-R2** | View Reservation List | Cashier / Order Staff | `reservation:view` | Counter-scoped, time-sorted, filter by date + status. Highlight HOLDING rows. Performs lazy-expire (BR-R8). |
| **UC-R3** | Seat from Reservation | Cashier / Order Staff | `reservation:seat` | Only when `BOOKED` and `now ≤ window_end`. Realtime floor plan, booked tables auto-selected and editable. Confirm → one independent Ticket per selected table, linked to the reservation; reservation → `ARRIVED`; tables → Occupied. |
| **UC-R4** | Cancel Reservation | Cashier / Order Staff | `reservation:cancel` | Only when `BOOKED`. Select CancellationReason (BR-CR1) → `CANCELLED`, releases hold. |
| **UC-R5** | Floor Plan Projected-to-Time | Cashier / Order Staff | `reservation:create` | Sub-flow of UC-R1 step 2. Input: counter + target time. Output: per-table state at that time (FREE / RESERVED-overlap) + the reservations covering the window. |
| **(BR-R2)** | Walk-in warning | Cashier / Order Staff | (part of OpenTicket) | Opening a walk-in on a table currently HOLDING → warning "this table has a booking at HH:MM", **allowed**. Not a standalone UC; an informational check in the Open-Ticket flow. |

Changes vs original docs:
- UC-R6 (auto-finalize no-show cron) — **removed**; replaced by lazy-expire in UC-R2.
- Permissions: code currently has `reservation:create` + `reservation:cancel`; **add `reservation:view` + `reservation:seat`** (4 total).

### 4.1 Detailed flows

**UC-R1 — Create Reservation**
1. Staff opens SC-B from the Reservation List. Picks a **target time** (mandatory; date + time). Nothing else is enabled until time is chosen.
2. System computes, for the staff's current counter, the floor plan **as projected to that target time**: each table is FREE or RESERVED-overlap (a `BOOKED` reservation whose window covers the target time). It also returns the list of those overlapping reservations to overlay on the plan (UC-R5).
3. Staff ticks one or more tables. A table is flagged **RESERVED-overlap and not selectable** when ticking it would violate BR-R1 — i.e. an existing `BOOKED` reservation's window would overlap the new reservation's window `[target − pre, target + grace]` (interval overlap, *not* point-in-time at the target instant; see the worked example in §6 BR-R1). The overlapping reservation(s) are overlaid on the plan so staff can see who holds the table and pick a different time or table. All ticked tables must belong to the same counter (BR-R6) — enforced because the projection is already counter-scoped.
4. Staff enters customer name, phone, guest count, optional note.
5. On Save: validate BR-R6 + BR-R1 (per table). Insert `Reservation` (`Status = BOOKED`, denormalized `CounterId`, auto-generated `Code`) and one `ReservationTable` per ticked table, in one transaction. Bump `FLOOR_PLAN` (counter). Write AuditLog `CREATE`.

**UC-R2 — View Reservation List (with lazy-expire)**
1. Staff opens SC-A. Query is filtered by current counter and a date (default today), optionally by status.
2. **Lazy-expire (BR-R8):** before returning, the handler finds all `BOOKED` reservations in scope whose `now > window_end` and sets them to `NotArrived` (bump `Version`/`UpdatedAt`, bump `FLOOR_PLAN`, AuditLog `NOT_ARRIVED` with a system actor). This is a deliberate write-on-read, contained in this one handler.
3. Return rows sorted by target time; mark each row's derived phase (PENDING / HOLDING / EXPIRED) for `BOOKED` rows so the UI can highlight HOLDING and dim past ones.

**UC-R3 — Seat from Reservation**
1. From SC-C the staff taps Seat. Available only if `Status = BOOKED` and `now ≤ window_end`.
2. SC-D shows the **realtime** floor plan of the counter; the reservation's booked tables are pre-selected. Staff may deselect booked tables and/or select other free tables (same counter).
3. On Confirm, in one transaction with a pessimistic lock (see §7):
   - Re-read and re-check `Status = BOOKED` and `now ≤ window_end` (reject otherwise — concurrent expire/cancel).
   - For each selected table: open a Ticket exactly like a normal Open-Ticket (snapshot `ServiceChargePercent` / `ServiceChargeVatPercent` from the table's Area), set `Ticket.ReservationId`, set Table → Occupied.
   - Set reservation `Status = ARRIVED`, `ArrivedAt = now`.
   - Booked tables that were *not* selected simply stop being held (the reservation is no longer `BOOKED`).
   - Bump `FLOOR_PLAN` (counter) after SaveChanges. AuditLog `SEAT`.
4. UI returns to Cashier Main; the seated tables now read Occupied.

**UC-R4 — Cancel Reservation**
1. From SC-C, only when `BOOKED`. Staff selects a CancellationReason (BR-CR1) and optional note.
2. Set `Status = CANCELLED`, `CancelledAt = now`, store reason/note (optimistic `Version`). Bump `FLOOR_PLAN`. AuditLog `CANCEL`.

**UC-R5 — Floor Plan Projected-to-Time** (read-only sub-flow)
- Input: counter + target time. For each table in the counter, mark RESERVED-overlap iff ∃ `BOOKED` reservation r with that table in `ReservationTable` and `target_time ∈ [r.TargetTime − pre, r.TargetTime + grace]`. Also return those reservations. No writes.

---

## 5. Entity Design

```
        ┌──────────────────────────────────────────────┐
        │              Reservation                       │
        │  Id, Code, CounterId (denorm), CustomerName,   │
        │  CustomerPhone, GuestCount, Note, TargetTime,  │
        │  Status, ArrivedAt, CancelledAt,               │
        │  CancellationReasonId, CancellationNote,       │
        │  CreatedByStaffId, Version, CreatedAt,         │
        │  UpdatedAt                                     │
        └───────┬───────────────────────────┬───────────┘
                │ 1                        1 │
   booked tables│ N (many-to-many)           │ N  tickets opened at seat
                ▼                            ▼
      ┌────────────────────┐      ┌──────────────────────────┐
      │ ReservationTable    │      │ Ticket (existing)         │
      │  ReservationId  FK  │      │  + ReservationId (FK,     │
      │  TableId        FK  │      │     nullable) ◄── ADD      │
      │  CreatedAt          │      │  set at seat = actual     │
      │  PK(ResId, TableId) │      │  seated table             │
      └────────────────────┘      └──────────────────────────┘
```

**Reservation** (modify existing `src/Rpom.Domain/Reservation/Reservation.cs`):
- **Remove** `TableId` (moves to `ReservationTable`) and `LinkedTicketId` (moves to `Ticket.ReservationId`).
- **Add** `CounterId` (denormalized; all tables share it — matches Ticket's denormalized `CounterId`/`AreaId`).
- **Add** `Version int` (optimistic concurrency for Cancel; per ERD §2 / CLAUDE.md §10).
- Keep: `Code`, `CustomerName`, `CustomerPhone`, `GuestCount`, `Note`, `TargetTime`, `Status`, `ArrivedAt`, `CancelledAt`, `CancellationReasonId`, `CancellationNote`, `CreatedByStaffId`, `CreatedAt`, `UpdatedAt`.

**ReservationTable** (new junction — booking intent, many-to-many):
- `ReservationId` FK → Reservation
- `TableId` FK → Table
- `CreatedAt`
- PK `(ReservationId, TableId)`
- Source of truth for BR-R1 overlap, UC-R5 projection, and the floor-plan hold badge.

**Ticket** (existing — add one column):
- `ReservationId bigint NULL` FK → Reservation. Set at seat. Reservation→tickets = `Tickets WHERE ReservationId = X`. Chosen over a junction because the relationship is 1-N (a ticket originates from at most one reservation), so the FK belongs on the many side.

**ReservationStatus** (`src/Rpom.Domain/Reservation/ReservationStatus.cs`): add `NotArrived`.
```
BOOKED ──seat──► ARRIVED
       ──cancel─► CANCELLED        (manual cancel of a BOOKED reservation; reason required)
       ──lazy───► NotArrived       (now > window_end; set on read of the list)
```
Derived phases inside BOOKED (computed, never stored): PENDING / HOLDING / EXPIRED — used only for UI highlighting.

**Reservation Config:** existing config codes `reservation.pre_buffer_minutes`, `reservation.grace_period_minutes` (defaults 30/30), stored in the DB config-value table and read via the existing config cache. No new table.

---

## 6. Business Rules

| BR | Rule |
|---|---|
| **BR-R1** | On **each table**, two `BOOKED` reservations must not have overlapping hold windows. On create, for each ticked table reject if ∃ another `BOOKED` reservation on that table with `[t−pre, t+grace] ∩ [t'−pre, t'+grace] ≠ ∅`. This is **interval overlap**, not point-in-time. *Worked example:* existing 18:00 (window [17:30, 18:30]) and new 18:45 (window [18:15, 19:15]) overlap at [18:15, 18:30] → reject, even though 18:45 alone is outside the existing window. The SC-B projection marks would-overlap tables as non-selectable on this same rule, so the plan and the save validation always agree. Only `BOOKED` considered (`ARRIVED` is already a ticket; `CANCELLED`/`NotArrived` ignored). |
| **BR-R2** | Opening a walk-in on a table currently HOLDING → warning, **always allowed**. OpenTicket is **not** coupled to reservations; the warning is informational, sourced from floor-plan data. |
| **BR-R3** | A table is "held" ⇔ ∃ `BOOKED` reservation on it and `now ∈ window`. Computed at read time; no placeholder ticket. |
| **BR-R5** | Seat opens one Ticket per actual selected table, sets `Ticket.ReservationId`, reservation → `ARRIVED` + `ArrivedAt`, tables → Occupied. |
| **BR-CR1** | `CANCELLED` requires a CancellationReason + AuditLog. |
| **BR-R6** *(new)* | All tables in a reservation must share the same `CounterId`. Enforced by the counter-scoped projection + validator. |
| **BR-R7** *(new)* | Seat allowed only when `Status = BOOKED` **and** `now ≤ window_end`. Past window (`NotArrived`) → Seat hidden; handle as walk-in. |
| **BR-R8** *(new)* | Lazy-expire: loading the Reservation List sets every in-scope `BOOKED` with `now > window_end` to `NotArrived` (bump version + AuditLog `NOT_ARRIVED`). No cron. |
| ~~BR-R4~~ | Removed — replaced by BR-R8. |

---

## 7. Business Logic & Cross-cutting

**Hold window (per table):**
```
held(table, now) ⇔ ∃ r BOOKED: table ∈ r.tables AND now ∈ [r.TargetTime − pre, r.TargetTime + grace]
```
`pre`/`grace` from Reservation Config (DB).

**Concurrency (per CLAUDE.md §10):**
- **Seat (UC-R3)** = multi-step check-act across reservation + tickets + tables → **pessimistic lock** (`SELECT … FOR UPDATE` on the reservation row and the target table rows inside the transaction); re-check BR-R7 after locking.
- **Cancel (UC-R4)** = single-row edit → **optimistic `Version`** (EF `IsConcurrencyToken`).
- **Lazy-expire (BR-R8)** = set-based UPDATE of expired `BOOKED` rows; idempotent (only flips rows still `BOOKED` past window).
- Do not mix optimistic + pessimistic on the same row.

**Versioning / polling (per RPOM_Versioning_Strategy):**
- `FLOOR_PLAN` (per counter) bumped on Create / Seat / Cancel / Expire — the floor plan renders the "đã đặt" badge.
- Reservation List polled via `Reservation.UpdatedAt` cursor + counter + date filter.
- Bump only **after** a successful `SaveChangesAsync` (CLAUDE.md §9).

**AuditLog actions:** `CREATE`, `SEAT`, `CANCEL`, `NOT_ARRIVED` (system actor for lazy-expire). No AuditLog for read-only queries or for UC-R5 projection.

**Permissions:** `reservation:view`, `reservation:create`, `reservation:seat`, `reservation:cancel`. Add `view` + `seat` to `Permissions.cs` and seed defaults (Cashier, Order Staff, Manager) in `AccessSeeder`/`RolePermissionDefaults`.

**Code generation:** `Reservation.Code` auto-generated as `R-YYYY-NNN` (per-year sequence). No manual entry in v1.

**Snapshots:** none new — customer info is plain text already on the reservation; ticket SC snapshots happen in the normal Open-Ticket path during seat.

---

## 8. Out of scope (v1)

- No cron/background job for no-show (lazy-on-read only).
- No automatic merge of the per-table tickets opened at seat (cashier uses existing Merge Bills if wanted).
- No customer master / loyalty lookup (anonymous, plain-text phone — RPOM Glossary §9).
- No deposit/pre-payment on reservation.
- No enforced relation between `GuestCount` and number of tables.

---

## 9. CLAUDE.md / docs reconciliation needed after implementation

- Update Glossary §4.8/§6.7/§7.5, Business Flows F6, Features/Requirements, and the Logical ERD to reflect: multi-table (junction), `Ticket.ReservationId`, `NotArrived` status, lazy-expire (no cron), non-blocking hold, counter scope, 4 permissions.
- Add a CHANGES.md entry.
