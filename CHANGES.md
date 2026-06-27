# Rpom Backend — Changelog

Branch: `feature/reservation`

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
| `FLOOR_PLAN` | Ticket open/send/cancel/transfer/merge/split, Table lock/unlock, OrderItem lifecycle |
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

---

## 13. Order rollup fix (Cancel/MarkDone)

- **Bug**: roll-up Order→DONE query item-status bằng projection **trước** `SaveChangesAsync` → item vừa đổi đọc ra state cũ → order kẹt SENT/PROCESSING. Thêm: check phạm vi cả ticket thay vì per-order.
- **Fix**: `src/Rpom.Application/Cashier/OrderRollup.cs` (helper per-order). `CancelOrderItem` + `MarkDoneOrderItem` SaveChanges trước rồi mới roll-up.
- **Tests**: +3 trong `PricingIntegrationTests.cs` (one-by-one cancel/markdone, multi-order batch).

---

## 14. Cancel Ticket (OPEN → CANCELLED)

- **Use case**: `src/Rpom.Application/Cashier/CancelTicket/CancelTicket.cs`
- **Endpoint**: `POST /api/cashier/tickets/{ticketId:long}/cancel` body `{ managerStaffId, cancellationReasonId, cancellationNote? }`, perm `ticket:cancel`.
- Huỷ nguyên phiếu OPEN khi **bill rỗng** (mọi OrderItem đã CANCELLED) và **không có payment** PENDING/SUCCESS. Manager (Owner/Manager, active) bắt buộc duyệt qua `managerStaffId`.
- Side effects: auto-drop DRAFT cart (order DRAFT → DELETED), set `CancelledAt`/`CancellationReasonId`/`CancellationNote`/`ManagerStaffId`, ghi AuditLog `CANCEL`, release table lock, bump `FLOOR_PLAN`. **Không** refund (đã chặn payment SUCCESS).
- **Errors mới**: `Ticket.HasActiveItems`, `Ticket.HasPendingPayment`, `Ticket.HasSuccessfulPayment`, `Ticket.InvalidCancellationReason`, `Ticket.InvalidManager`.
- **Permission**: grant `ticket:cancel` cho role **Cashier** (người giữ table lock gọi endpoint; manager duyệt trong body).
- **Tests**: +8 trong `PricingIntegrationTests.cs` (empty cancel + lock release + audit, active item blocked, after-all-cancelled OK, pending/success payment blocked, draft cart dropped, non-manager blocked, inactive reason blocked).

---

## 15. Discount Engine → Percent-Based (F2-style)

- **Spec**: `~/CapstoneProject/docs/superpowers/specs/2026-06-14-discount-percent-engine-design.md`
- Chuyển discount engine từ **frozen per-line amount** sang **percent-based**, re-derive mỗi recompute. Nguồn sự thật = `Ticket.DiscountPolicyId`.
- **`DiscountResolver`** (`src/Rpom.Application/Cashier/Pricing/DiscountResolver.cs`): pure, fixed→% (`fixedValue/currentSubtotal`), re-check điều kiện, cap ≤100.
- **`TicketRecomputeService`**: mỗi recompute re-derive % của policy đang gắn (attached-policy only, **không** re-select); tụt dưới ngưỡng → gỡ `DiscountPolicyId`; dòng net-âm → discount 0. **`PricingCalculator`** percent-only (bỏ `ForcedLineDiscountAmount`/`ForcedTicketDiscountAmount`).
- **`ApplyDiscountPolicy`** + **`SendOrder` auto-apply**: chỉ **chọn + gắn** policy; recompute lo toàn bộ math. Auto-apply vẫn ghi AuditLog `APPLY_DISCOUNT`.
- **Schema**: discount-percent cột nới `decimal(5,2)` → `decimal(9,6)` (migration `WidenDiscountPercentPrecision`). VAT%/SC% giữ `(5,2)`.
- **Behavior change**: refund/cancel làm subtotal tụt dưới ngưỡng → discount **tự gỡ** (trước đây giữ nguyên). FIXED discount giữ đúng số tiền khi bill đổi (vd order thêm → % co lại, tiền giảm vẫn ~100k).
- **Docs**: CLAUDE.md §12 (bỏ exact-amount guarantee) + §5 (precision discount %).
- **Tests**: +5 unit `DiscountResolverTests.cs`, +2 integration (re-derive giữ amount, gỡ khi dưới ngưỡng). Full suite 169 pass; assertion exact cũ (291,600 / 972,000) không đổi.

---

## 16. Refund Line (trả hàng)

- **Spec**: `~/CapstoneProject/docs/superpowers/specs/2026-06-14-discount-percent-engine-design.md` §10.
- Trả món đã/đang nấu = **dòng OrderItem qty ÂM** trỏ về dòng gốc qua `OriginalOrderItemId`. Cơ chế: `AddRefundLine` tạo CartItem DRAFT âm → **SendOrder** (sẵn có) materialize thành OrderItem âm. KHÔNG có endpoint `/refund` gửi riêng.
- **Use case**: `src/Rpom.Application/Cashier/AddRefundLine/AddRefundLine.cs`.
- **Endpoint**: `POST /api/cashier/tickets/{ticketId:long}/order-items/{orderItemId:long}/refund-line` body `{ quantity, cancellationReasonId, cancellationNote? }` (`quantity` dương, server lưu âm), perm `order_item:refund_line`.
- **Guards**: ticket OPEN + giữ lock; gốc thuộc ticket; gốc PROCESSING/READY/DONE (PENDING dùng Cancel) → `OrderItem.NotRefundable`; gốc là dòng dương non-refund → `OrderItem.CannotRefundRefund`; reason active → `Ticket.InvalidCancellationReason`; `quantity ≤ gốc − (đã refund committed + draft)` → `OrderItem.RefundQuantityExceeded`.
- **Snapshot** từ OrderItem gốc (ItemCode/Name, Uom*, UnitPrice, ChoicePrice, VAT%, SC%). Dòng refund qua SendOrder → status **PENDING** (chạy lifecycle bếp), ghi AuditLog `REFUND` (EntityId = OrderItem gốc) lúc gửi.
- **Schema**: thêm `OriginalOrderItemId`, `CancellationReasonId`, `CancellationNote` vào `CartItem` (migration `AddRefundFieldsToCartItem`).
- **AddCartItem**: note-free merge loại dòng refund (`OriginalOrderItemId == null`) để add món thường không gộp nhầm vào dòng âm.
- **Tiền/discount**: do percent-engine lo (dòng âm → tiền âm, net-negative cleanup). VD trả 1/3 phở → bill còn net 2 = 108,000.
- **Permission**: grant `order_item:refund_line` cho role **Cashier**.
- **Tests**: +6 integration (tạo dòng âm linked, PENDING-original/exceed/inactive-reason blocked, SendOrder materialize+audit+credit, không merge nhầm). Full suite 175 pass.

---

## 17. Merge Ticket (E3 — Gộp hoá đơn)

- **Use case**: `src/Rpom.Application/Cashier/MergeTicket/MergeTicket.cs`
- **Endpoint**: `POST /api/cashier/tickets/merge` body `{ sourceTicketId, destinationTicketId }`, perm `ticket:merge`.
- Chuyển toàn bộ Orders (SENT/PROCESSING/DONE) + Payments (non-Deleted) từ source → dest. Cả 2 OPEN, cùng Area, CashDrawer OPEN, không PENDING payment.
- Sau khi move: tạo bản copy CANCELLED của mọi Orders/Items/Payments trên source (audit trail). Source → CANCELLED với reason `MERGE`. Guest count source cộng vào dest. Nếu source table không còn OPEN ticket nào → table → Available. Giải phóng table lock source.
- Audit: `MERGE` trên source + `MERGE_RECEIVE` trên dest. Bump `FLOOR_PLAN`, `KITCHEN`, `PRICING`.
- **Errors**: `MergeSameTicket`, `MergeDifferentArea`.
- **Permission**: grant `ticket:merge` cho role **Cashier**, **Order Staff**.

---

## 18. Split Ticket (E4 — Tách hoá đơn)

- **Use case**: `src/Rpom.Application/Cashier/SplitTicket/SplitTicket.cs`
- **Endpoint**: `POST /api/cashier/tickets/split` body `{ sourceTicketId, destinationTicketId?, destinationTableId?, guestCount?, items: [{ orderItemId, quantity }] }`, perm `ticket:split`.
- Chuyển selected OrderItems từ source → dest. 2 mode dest: ticket có sẵn (OPEN, cùng Area, không PENDING payment, không bị staff khác giữ) hoặc mở ticket mới trên bàn (cùng Area, snapshot SC% từ Area).
- Full qty → re-parent dòng; partial → giảm source, tạo dòng copy ở dest (copy toàn bộ snapshot: giá, bếp status, modifier details). Món giữ nguyên trạng thái bếp.
- Tạo Order mới trên dest với notes `"Tách từ hoá đơn {code}"`. Source Orders: hết active item → DELETED; còn item → roll up.
- **Payment không di chuyển** — chỉ chuyển món. Source phải `PaidAmount = 0` và không PENDING payment.
- Recomputed cả 2 ticket. Audit `SPLIT` trên source. Bump `FLOOR_PLAN`, `KITCHEN`, `PRICING`.
- **Errors**: `SplitDestinationInvalid` (cần đúng 1 trong 2 mode), `SplitSameTicket`, `SplitDifferentArea`, `SplitItemInvalid` (món không thuộc ticket/đã huỷ/trùng id), `SplitQuantityExceeds`, `SplitNoItems`, `SplitSourcePaid`.
- **Permission**: grant `ticket:split` cho role **Cashier** (seed: `AccessSeeder`).

---

## 19. Split Ticket Preview (dry-run, không ghi DB)

- **Spec**: `~/CapstoneProject/docs/superpowers/specs/2026-06-20-split-ticket-cashier-spec.md`.
- **Mục đích**: FE màn tách (2-pane) chỉ quản số lượng, **không tính tiền ở client** (CLAUDE.md §12). Tổng tiền 2 phiếu "sau khi tách" lấy từ endpoint preview real-time khi cashier chỉnh số lượng.
- **Use case**: `src/Rpom.Application/Cashier/SplitTicketPreview/SplitTicketPreview.cs` (**Query**, không qua TransactionPipeline auto-commit).
- **Endpoint**: `POST /api/cashier/tickets/split/preview` body y hệt `/split`, trả `{ sourceTotalAmount, destinationTotalAmount, movedItemCount }`, perm `ticket:split`.
- **Cơ chế dry-run**: tái dùng **handler `SplitTicket` thật** (gọi trực tiếp qua `IRequestHandler<...>`, không qua MediatR pipeline) trong 1 transaction rồi **ROLLBACK** → số preview == số commit tuyệt đối, không công thức song song, không temp table.
  - Bọc trong `IExecutionStrategy.ExecuteAsync` (Npgsql retry yêu cầu); `ChangeTracker.Clear()` đầu mỗi lần retry (rollback không revert change tracker).
  - **Không** side-effect: AuditLog / version bump / outbox đều nằm trong transaction → rollback xoá sạch.
  - Lưu ý: mode "mở phiếu mới" tiêu hao sequence `Ticket.Id` (Postgres sequence không rollback) — chỉ tạo lỗ hổng id.
- **IDbContext**: thêm `ChangeTracker` vào abstraction (phục vụ retry-safety của dry-run).
- **GetTicketDetails**: thêm `TotalDiscountAmount` vào `OrderedItem` (cột "Giảm" của pane trái).
- **Tests**: +2 integration `SplitTicketPreviewTests.cs` (preview không ghi DB + số khớp split thật; propagate lỗi `SplitSourcePaid`). Full suite **177 pass**.

---

## 20. Module/Page UI Authorization (2026-06-20)

- **Spec**: `~/CapstoneProject/docs/superpowers/specs/2026-06-20-module-page-ui-authorization-design.md`. **Plan**: `docs/superpowers/plans/2026-06-20-module-page-ui-authorization.md`.
- Tầng phân quyền **điều hướng** (Module → Page, per-account), **độc lập** với permission. Permission vẫn là cổng server cho mọi API; Module/Page chỉ phục vụ FE route-guard + sidebar. Server **không** hard-enforce page access trên data API (chủ ý — non-goal trong spec §1).
- **Entities** (mirror bộ ba Permission): `Module` / `Page` / `StaffAccountPageAccess` ở `src/Rpom.Domain/Access/`. Grant ở mức Page (atomic); module "thấy được" khi có ≥1 page. Migration `AddModulePageAuthorization` (3 bảng).
- **Catalog code**: `Modules.cs` (4 module), `Pages.cs` (25 page), `RolePageDefaults.cs` (template page mặc định theo role) ở `src/Rpom.Application/Access/`.
- **Seeder**: `AccessSeeder` seed 4 module + 25 page + permission mới `page_access:assign`; bootstrap Owner nhận **toàn bộ page** (+ `SyncOwnerPageAccessAsync` lúc restart).
- **Endpoints** (`.WithTags("Access")`):
  - `GET /api/access/my-menu` — auth bất kỳ staff; trả cây module→page account hiện tại truy cập được (nguồn cho sidebar + route guard).
  - `GET /api/access/staff-accounts/{id}/page-access` — perm `page_access:assign`; full catalog + cờ `granted` cho 1 account (lưới checkbox admin).
  - `PUT /api/access/staff-accounts/{id}/page-access` — perm `page_access:assign`; **full-replace** tập page, ghi `AuditLog` (UPDATE/StaffAccount), bump `VersionScopes.Access`.
  - `GET /api/access/role-page-defaults/{roleCode}` — perm `page_access:assign`; template page mặc định theo role (pre-fill grid, reset-to-default).
- **Invalidate**: FE poll `GetVersions`; `ACCESS` đổi → fetch lại `my-menu`. Chỉ PUT page-access bump version.
- **Integration point (chưa làm)**: `CreateStaffAccount` sau này gọi `RolePageDefaults.ForRole(...)` để seed page access khởi tạo.
- **Tests**: +10 integration (`PageAccessTests`) — my-menu lọc theo grant, full grid + granted flags, full-replace add/remove + persist, unknown page/account, version bump, role default. Full suite **187 pass**.

---

## 21. Account Management & Authorization Admin — Backend (2026-06-21)

- **Spec**: `~/CapstoneProject/docs/superpowers/specs/2026-06-20-account-management-authorization-admin-design.md`. **Plan**: `docs/superpowers/plans/2026-06-20-account-management-authorization-admin.md`.
- API backend cho màn ERP quản lý account + cấp quyền (Role-filtered navigation + permission). **Không thêm entity/migration** — tái dùng `StaffAccount`/`Role`/`Permission`/`Module`/`Page` + bộ ba page-access. FE (NextERP) ở phase riêng.
- **Use cases mới** (`src/Rpom.Application/Access/`), endpoint `.WithTags("Access")`:
  - `GET /api/access/roles` — list role + số account mỗi role (cây trái + selector). Perm `staff_account:manage`.
  - `GET /api/access/staff-accounts?roleId=&search=&pageNumber=&pageSize=` — grid account (paged). Perm `staff_account:manage`.
  - `GET /api/access/staff-accounts/{id}` — chi tiết account. Perm `staff_account:manage`.
  - `POST /api/access/staff-accounts` — tạo account (BCrypt hash, AuditLog CREATE, bump ACCESS). Perm `staff_account:manage`.
  - `PUT /api/access/staff-accounts/{id}` — sửa fullname/phone/email/role/isActive/isLocked (username immutable; AuditLog UPDATE, bump ACCESS). Perm `staff_account:manage`.
  - `PUT /api/access/staff-accounts/{id}/password` — reset mật khẩu (BCrypt, AuditLog RESET_PASSWORD, KHÔNG bump). Perm `staff_account:manage`.
  - `GET /api/access/staff-accounts/{id}/permissions` — full permission catalog + cờ granted (mirror page-access). Perm `permission:assign`.
  - `PUT /api/access/staff-accounts/{id}/permissions` — full-replace permission grants (AuditLog UPDATE, bump ACCESS). Perm `permission:assign`.
  - `GET /api/access/role-permission-defaults/{roleCode}` — template permission mặc định theo role. Perm `permission:assign`.
- **Catalog/Errors mới**: `RolePermissionDefaults.cs` (template permission theo role); `AccessErrors.UsernameDuplicate` / `RoleNotFound` / `UnknownPermissionCode`.
- **AuditLog append-only**: `CreateStaffAccount` save account trước (lấy id) rồi mới insert AuditLog (không UPDATE), 2 save trong cùng transaction.
- **Tests**: +21 integration (`AccountManagementTests`). Full suite **208 pass** (Application). Route smoke: 3 endpoint mới trả 401 (đã register + auth).

---

## 22. Reservation Redesign (2026-06-27)

- **Spec**: `~/CapstoneProject/docs/superpowers/specs/2026-06-27-reservation-redesign-design.md`. **Plan**: `docs/superpowers/plans/2026-06-27-reservation-redesign.md`.
- Phone booking feature for Cashier + Order Staff, counter-scoped. Multi-table reservations via new `ReservationTable` junction table.
- **Schema changes**:
  - **Migration**: `AddReservationRedesign` — drops legacy `Reservation.TableId` + `Reservation.LinkedTicketId` columns; adds `Reservation.CounterId` (FK, scoped), `Reservation.Version` (optimistic concurrency), new junction `ReservationTable` (FK pair → Reservation + Table), new nullable FK `Ticket.ReservationId`.
- **New status**: `NOT_ARRIVED` (set lazily on list read — no background cron). Hold window is non-blocking (walk-ins permitted).
- **5 endpoints** under `POST/GET /api/reservations/...`:
  - `CreateReservation` (UC-R1) — creates phone booking.
  - `ListReservations` (UC-R2) — list counter-scoped, auto-mark late NOT_ARRIVED.
  - `GetReservationDetails` (UC-R3) — projection; read-only, no mutation.
  - `SeatReservation` (UC-R4) — link tables + open seated ticket (one-shot; tables become OCCUPIED).
  - `CancelReservation` (UC-R5) — mark CANCELLED; release table locks.
- **Use cases** in `src/Rpom.Application/Reservations/<UseCase>/<UseCase>.cs` (per-file pattern per CLAUDE.md §2).
- **Permissions** (4 new): `reservation:view`, `reservation:create`, `reservation:seat`, `reservation:cancel` (seeded in `AccessSeeder`).
- **AuditLog**: `CREATE` (UC-R1), `SEAT` (UC-R4), `CANCEL` (UC-R5). Bump `FLOOR_PLAN` (tables, tickets).
- **⚠️ Doc Reconciliation Pending**: Canonical docs (`RPOM_Glossary.md` §4.8/§6.7/§7.5, `RPOM_Business_Flows.md` F6, `RPOM_Features_and_Screens.md`, `RPOM_Requirements.md`, Logical ERD) still describe pre-redesign single-table model. Reconciliation deferred post-merge.
