# Rpom Backend — Changelog

Branch: `feature/cashier-pricing`

---

## 1. Infrastructure

### 1.1 Transaction Pipeline (ACID)
- **File**: `src/Rpom.Application/Abstraction/Behaviors/TransactionPipelineBehavior.cs`
- Mọi `IBaseCommand` handler được auto-wrap trong DB transaction qua `CreateExecutionStrategy` (tương thích Npgsql EnableRetryOnFailure).
- Handler gọi `SaveChangesAsync` nhiều lần → tất cả atomic (all-or-nothing).

### 1.2 ConcurrencyVersionInterceptor
- **File**: `src/Rpom.Infrastructure/Database/ConcurrencyVersionInterceptor.cs`
- `SaveChangesInterceptor`: auto-increment `Version` trên mọi Modified entity.
- `ExceptionHandlingPipelineBehavior` catch `DbUpdateConcurrencyException` → 409.

### 1.3 GetMenu — Ancestor Categories
- **File**: `src/Rpom.Application/Cashier/GetMenu/GetMenu.cs:137-147`
- Sau khi filter visible categories, extract ancestor IDs từ `Path` của mỗi category.
- Include các ancestor còn thiếu vào response để FE build được drill-down tree.
- VD: "Bia" (DOUONG_BIA) path="1;3;4;" → ancestors [1,3] → thêm "Hàng bán" + "Đồ uống".

---

## 2. Cash Drawer

### 2.1 ShiftId trên CashDrawerSession
- **Entity**: `src/Rpom.Domain/Sales/CashDrawer/CashDrawerSession.cs`
- **FK**: `ShiftId → Shifts.Id` (Restrict)
- **Migration**: `20260607232923_AddShiftIdToCashDrawerSession`
- Mở ca (`OpenCashDrawer`) yêu cầu `ShiftId`, validate shift tồn tại + active.
- `GetCurrentCashDrawer` response trả `shiftId` + `shiftName`.

### 2.2 OpenTicket — ShiftId từ Drawer
- **File**: `src/Rpom.Application/Cashier/OpenTicket/OpenTicket.cs`
- Bỏ `ShiftId` khỏi request. Server tự suy từ OPEN cash drawer.
- `CashDrawerSessionId` và `ShiftId` được set từ drawer đang mở.

---

## 3. Order Item Lifecycle (4 APIs mới)

Tất cả handler trong `src/Rpom.Application/Cashier/{Action}OrderItem/`, endpoint trong `src/Rpom.Api/Endpoints/Cashier/Tickets/`.

| API | Route | Transition | Permission |
|---|---|---|---|
| **StartCook** | `POST .../order-items/start-cook` | PENDING → PROCESSING | `order_item:start_cooking` |
| **MarkReady** | `POST .../order-items/mark-ready` | PROCESSING → READY | `order_item:mark_ready` |
| **MarkDone** | `POST .../order-items/mark-done` | READY → DONE | `order_item:mark_done` |
| **Cancel** | `POST .../order-items/cancel` | PENDING → CANCELLED | `order_item:cancel_pending` |

### Business rules:
- **StartCook**: chỉ từ PENDING. Nếu Order đang SENT → bump lên PROCESSING.
- **MarkReady**: chỉ từ PROCESSING. No side effects.
- **MarkDone**: chỉ từ READY. Nếu TẤT CẢ OrderItem trên ticket đã terminal (DONE/CANCELLED) → đóng Order → DONE.
- **Cancel**: chỉ từ PENDING (đã nấu → refund, không cancel). Nếu tất cả terminal → đóng Order → DONE. Gọi `TicketRecomputeService` để cập nhật totals.
- Tất cả request body: `{ orderItemIds: number[] }` — batch support.
- Tất cả require table lock + ticket OPEN.

---

## 4. Set Menu Validator

### 4.1 SetMenuValidator — fixed duplicate qty bypass
- **File**: `src/Rpom.Application/Cashier/AddCartItem/SetMenuValidator.cs:69-82`
- Aggregate qty by modifier trước khi check `MinPerModifier ≤ qty ≤ MaxPerModifier`.
- Trước đây: gửi 2 dòng cùng modifier (mỗi dòng qty=1) → từng dòng pass, tổng qty=2 > MaxPerModifier=1 → bypass.
- Nay: `qtyByMod[itemId]` aggregate → check tổng.

### 4.2 SetModifiers — cross-validation
- **File**: `src/Rpom.Application/ChoiceCategories/SetModifiers/SetModifiers.cs:78-100`
- `MinPerModifier ≤ MaxPerModifier` (mỗi modifier)
- `MaxPerModifier ≤ MaxChoice` (của ChoiceCategory)
- `Σ MinPerModifier ≤ MaxChoice` (tổng min không vượt max của CC)
- Lỗi trong `ChoiceCategoryErrors.cs`.

---

## 5. GetTicketDetails — OrderingItem.vatPercent
- **File**: `src/Rpom.Application/Cashier/GetTicketDetails/GetTicketDetails.cs`
- `OrderingItem` record: thêm `VatPercent`.
- Cart item projection: thêm `c.VatPercent`.
- FE hiển thị % VAT trên mỗi dòng cart.

---

## 6. ListItems — isSetMenu flag
- **File**: `src/Rpom.Application/Items/ListItems/ListItems.cs`
- `Item` record thêm `bool IsSetMenu`.
- Projection: `IsSetMenu = x.SetMenu != null`.
- ERP filter "Đã là Set Menu" dùng flag này.

---

## 7. Permissions
- **File**: `src/Rpom.Application/Access/Permissions.cs`
- Thêm: `OrderItemMarkDone = "order_item:mark_done"` (POS group)
- Seed trong `AccessSeeder` + grant cho Cashier role trong `CashierDemoSeeder`.

---

## 8. Seed Data (CashierDemoSeeder + LookupSeeder)

### 8.1 Discount Policies (5 policies)
| Code | Type | Auto? | Condition | Apply |
|---|---|---|---|---|
| GIAM10 | TicketThreshold | Yes | Bill ≥200k/500k | 10%/15% PERCENT |
| GIAM100 | TicketThreshold | Yes | Bill ≥1M | 100k FIXED |
| GIAM_BIA | QuantityItem | No | 5 Heineken | 20% PERCENT |
| GIAM_PHO | QuantityItem | No | 3 Phở | 30k FIXED |
| GIAM_TUAN | TicketThreshold | No | Bill ≥500k Mon-Fri | 5% PERCENT |

### 8.2 VAT-included Items
- **Trà đá**: 5,000đ, VAT 10% included → basePrice ≈ 4,545đ
- **Cà phê đen**: 20,000đ, VAT 8% included → basePrice ≈ 18,519đ

### 8.3 Service Charge
- **Khu VIP**: `ServiceChargePercent = 5%`, `ServiceChargeVatPercent = 8%`

### 8.4 Set Menu
- **Combo Gà xối mỡ** (Cơm + Coca-Cola + Choice "Đổi nước")
- **Combo Phở bò tái** (Phở + Choice "Thêm món")

---

## 9. Tests

### 9.1 PricingIntegrationTests (7 tests)
- **File**: `tests/Rpom.Application.Tests/Cashier/PricingIntegrationTests.cs`
- `VatExcludedItem_HasCorrectPricing` — Phở 50k + 8% VAT = 108k
- `VatIncludedItem_HasCorrectPricing` — Trà đá 5k incl 10% = 5k
- `VatIncludedItem_MixedWithExcluded_SendOrder_TotalsMatch` — Phở + Trà đá = 113k
- `DiscountPercent_AutoApply_BillAboveThreshold` — 6 Phở ≥200k → 10% → 291.6k
- `DiscountFixed_AutoApply_BillAboveThreshold` — 20 Phở ≥1M → -100k → 972k
- `CancelOrderItem_RecomputesTicket` — Cancel 1/3 → 108k
- `PartialSend_KeptItemsInNewDraft` — Gửi partial → món còn lại ở draft mới

### 9.2 OrderingLoopTests (existing, updated)
- Cập nhật `OpenTicket.Command` bỏ ShiftId, drawer set `ShiftId`.

---

## 10. API Routes Summary

### Auth
| Method | Path | Auth |
|---|---|---|
| POST | `/api/auth/login` | AllowAnonymous |
| GET | `/api/auth/me` | Any authed |

### Lookups (pre-login)
| Method | Path | Auth |
|---|---|---|
| GET | `/api/lookups/counters` | AllowAnonymous |
| GET | `/api/lookups/kitchen-stations` | AllowAnonymous |
| GET | `/api/lookups/denominations` | Any authed |
| GET | `/api/lookups/shifts` | Any authed |

### Cash Drawer
| Method | Path | Auth |
|---|---|---|
| POST | `/api/cash-drawers` | `cash_drawer:open` |
| GET | `/api/cash-drawers/current?counterId=` | Any authed |

### Floor Plan & Tables
| Method | Path | Auth |
|---|---|---|
| GET | `/api/cashier/floor-plan?counterId=` | `cashier:floor_plan` |
| POST | `/api/cashier/tables/{id}/lock` | `ticket:open` |
| DELETE | `/api/cashier/tables/{id}/lock` | `ticket:open` |

### Tickets — CRUD
| Method | Path | Auth |
|---|---|---|
| POST | `/api/cashier/tickets` | `ticket:open` |
| GET | `/api/cashier/tickets/{id}` | `ticket:view_detail` |
| GET | `/api/cashier/tables/{id}/tickets` | `ticket:view_detail` |

### Cart — Order
| Method | Path | Auth |
|---|---|---|
| POST | `/api/cashier/tickets/{id}/cart-items` | `order:add_items` |
| PUT | `/api/cashier/tickets/{id}/cart-items/{cid}` | `order:add_items` |
| DELETE | `/api/cashier/tickets/{id}/cart-items/{cid}` | `order:add_items` |
| POST | `/api/cashier/tickets/{id}/send-order` | `order:send_kitchen` |

### Order Item Lifecycle
| Method | Path | Auth |
|---|---|---|
| POST | `/api/cashier/tickets/{id}/order-items/start-cook` | `order_item:start_cooking` |
| POST | `/api/cashier/tickets/{id}/order-items/mark-ready` | `order_item:mark_ready` |
| POST | `/api/cashier/tickets/{id}/order-items/mark-done` | `order_item:mark_done` |
| POST | `/api/cashier/tickets/{id}/order-items/cancel` | `order_item:cancel_pending` |

### Discount
| Method | Path | Auth |
|---|---|---|
| POST | `/api/cashier/tickets/{id}/apply-discount` | `ticket:apply_discount` |
| DELETE | `/api/cashier/tickets/{id}/discount` | `ticket:apply_discount` |

### Menu
| Method | Path | Auth |
|---|---|---|
| GET | `/api/cashier/menu?tableId=` | `cashier:view_menu` |

### Sync
| Method | Path | Auth |
|---|---|---|
| GET | `/api/sync/versions?scopes=` | Any authed |

---

## 11. Version Scopes

| Scope | Bumped by |
|---|---|
| `MENU` | Item, Category, Price thay đổi |
| `PRICING` | Discount apply/remove |
| `FLOOR_PLAN` | Ticket open/send/cancel/transfer, Table lock/unlock, OrderItem lifecycle |
| `KITCHEN` | StartCook, MarkReady, MarkDone, CancelOrderItem |
| `ACCESS` | Staff/Permission changes |
| `CONFIG` | System config changes |

---

## 12. Transfer Table (E2)

- **Spec**: `~/CapstoneProject/docs/superpowers/specs/2026-06-13-transfer-table-design.md`
- **Use case**: `src/Rpom.Application/Cashier/TransferTable/TransferTable.cs`
- **Endpoint**: `POST /api/cashier/tickets/{ticketId:long}/transfer-table` body `{ targetTableId }`, perm `ticket:transfer`.
- Chuyển ticket OPEN sang bàn khác **cùng counter**; lock bàn nguồn; bàn đích → OCCUPIED, bàn nguồn giữ nguyên (defer free-bàn).
- **SENT OrderItem giữ nguyên giá** (snapshot, giống F2). Đổi Area → **clear sạch DRAFT cart** + service charge theo config.
- **Config mới** `transfer.use_target_area_service_charge` (BOOL, default `true`): true = re-snapshot SC từ Area đích + `TicketRecompute`; false = giữ SC của phiếu.
- **Errors mới**: `Ticket.TransferSameTable`, `Ticket.TransferCrossCounter`.
- **Tests**: `tests/Rpom.Application.Tests/Cashier/TransferTableTests.cs` (8 cases: same-area, cross-area SC true/false, not-open, same-table, cross-counter, no-lock, target-not-found).
