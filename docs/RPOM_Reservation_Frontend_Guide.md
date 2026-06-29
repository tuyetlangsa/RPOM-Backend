# RPOM — Reservation Frontend Guide

> Tài liệu cho **Frontend developer** để dựng giao diện module Đặt bàn (Reservation).
> Đi từ tổng quan → khái niệm → luồng màn hình → chi tiết từng API (request/response/lỗi) → bảng case.
> Backend đã implement đầy đủ 5 use case (UC-R1..R5), 48 test pass.
>
> Nguồn gốc thiết kế: `docs/superpowers/specs/2026-06-27-reservation-redesign-design.md`.
> Mọi `field name`, `enum`, `error code`, route trong doc này khớp với code thật.

---

## 1. Tổng quan

**Reservation = đặt bàn qua điện thoại.** Khách gọi tới nhà hàng, nhân viên ghi nhận booking cho một (hoặc nhiều) bàn vào một giờ trong tương lai.

- **Ai dùng:** Cashier và Order Staff (chung quyền). Reservation là **một module riêng**, vào từ một nút trên màn hình chính của Cashier.
- **Khách ẩn danh:** không có hồ sơ khách hàng — chỉ lưu tên + SĐT dạng text trên từng reservation.
- **Đặt nhiều bàn:** một reservation có thể giữ nhiều bàn (đoàn đông). Khi khách tới (seat), mỗi bàn mở **một phiếu (Ticket) độc lập**.
- **Scope theo quầy:** giống Ticket — `Counter → Area → Table → Reservation`. Mọi bàn trong một reservation phải cùng một Counter. Danh sách reservation luôn lọc theo Counter đang chọn.

### Bốn điều quan trọng nhất phải nắm
1. **Giữ bàn = chỉ cảnh báo, KHÔNG khóa.** Khi tới giờ, sơ đồ bàn hiện badge "đã đặt", nhưng nhân viên **vẫn được mở phiếu vãng lai** lên bàn đó (chỉ hiện cảnh báo). Mục đích: linh hoạt khi bàn sắp trống mà khách tới ngay.
2. **Quá giờ không đến → tự thành `NOT_ARRIVED`** (tính khi load danh sách, không có cron). Lúc đó **không seat từ reservation được nữa** — xử như khách vãng lai.
3. **Khi seat, bàn thực tế có thể khác bàn đã đặt.** Màn seat hiển thị sơ đồ bàn realtime, bàn đã đặt được tick sẵn, nhưng nhân viên đổi/thêm/bớt được. Phiếu mở ra gắn với **bàn thực tế ngồi**.
4. **Hủy (`CANCELLED`) chỉ dùng khi khách gọi báo hủy.** Cần chọn lý do.

---

## 2. Khái niệm cốt lõi

### 2.1. Hold window (cửa sổ giữ bàn)
Mỗi reservation có `targetTime` (giờ khách hẹn đến). Cửa sổ giữ bàn:

```
window = [ targetTime − preBufferMinutes , targetTime + graceMinutes ]
```

`preBufferMinutes` và `graceMinutes` là **config toàn nhà hàng** (mặc định 30 và 30 phút). FE không cần tự tính buffer — BE đã tính sẵn `phase` (xem dưới) trong danh sách. FE chỉ cần dùng `phase` để tô màu.

### 2.2. Status (lưu trong DB) — 4 giá trị
| Status | Ý nghĩa | Sinh ra khi |
|---|---|---|
| `BOOKED` | Đã đặt, chờ khách | Tạo mới (UC-R1) |
| `ARRIVED` | Khách đã tới, đã mở phiếu | Seat (UC-R3) |
| `CANCELLED` | Đã hủy (khách báo hủy) | Cancel (UC-R4), cần lý do |
| `NOT_ARRIVED` | Quá giờ không đến (no-show) | Tự động khi load danh sách nếu `BOOKED` đã quá `window_end` |

Chuyển trạng thái:
```
BOOKED ──seat────► ARRIVED       (mở phiếu, terminal)
       ──cancel──► CANCELLED     (khách hủy, terminal)
       ──(quá giờ, lazy)──► NOT_ARRIVED   (no-show, terminal)
```
`ARRIVED`, `CANCELLED`, `NOT_ARRIVED` đều là trạng thái cuối.

### 2.3. Phase (KHÔNG lưu — chỉ để hiển thị)
Chỉ áp dụng cho reservation đang `BOOKED`. BE trả `phase` trong danh sách:

| Phase | Nghĩa | Gợi ý UI |
|---|---|---|
| `PENDING` | `now < window_start` — chưa tới giờ giữ bàn | Hiện bình thường, badge xám/nhạt |
| `HOLDING` | `now ∈ window` — đang trong cửa sổ giữ bàn | **Highlight** (đây là khách "sắp/đang tới"), cho phép Seat |
| `EXPIRED` | `now > window_end` nhưng vẫn còn `BOOKED` tại thời điểm đọc | Trạng thái thoáng qua; lần load tiếp theo BE sẽ flip thành `NOT_ARRIVED` |

> Với status khác `BOOKED`, `phase = null`.

---

## 3. Authentication & Permissions

Mọi API yêu cầu JWT (header `Authorization: Bearer <token>` lấy từ `POST /api/auth/login`). Mỗi endpoint gắn 1 permission:

| Permission code | Dùng cho |
|---|---|
| `reservation:view` | Xem danh sách (GET list) |
| `reservation:create` | Tạo reservation + xem projection sơ đồ bàn |
| `reservation:seat` | Seat (mở phiếu từ reservation) |
| `reservation:cancel` | Hủy reservation |

Mặc định cả **Cashier, Order Staff, Manager** đều được cấp đủ 4 quyền. Nếu user thiếu quyền → API trả **403**; FE nên ẩn nút tương ứng.

> **Counter context không nằm trong JWT.** FE phải tự truyền `counterId` (quầy đang thao tác) vào các API. Lấy `counterId` từ context quầy hiện hành của ca làm (giống màn floor plan / ticket).

---

## 4. Response & Error format (chung toàn hệ thống)

### 4.1. Thành công
- `GET` / `POST` trả data → **200/201** với envelope:
```json
{ "isSuccess": true, "data": { /* payload */ } }
```
- `POST .../cancel` thành công → **204 No Content** (không có body).
- `POST /api/reservations` (create) → **201 Created**, có header `Location: /api/reservations/{id}` và body envelope như trên.

### 4.2. Lỗi (RFC 7807 ProblemDetails)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Reservation.TableOverlap",
  "detail": "Bàn đã có đặt bàn khác trùng khung giờ giữ bàn.",
  "status": 409
}
```
- `title` = **error code** (dùng để FE map logic / chọn thông báo).
- `detail` = mô tả tiếng Việt (có thể hiển thị trực tiếp cho user).
- `status`: `Validation → 400`, `NotFound → 404`, `Conflict → 409`, `UnAuthorized → 401`, thiếu permission → `403`.
- Lỗi validation (FluentValidation) còn kèm `errors: [{ code, description }]` trong body.

> **Quy ước FE:** bắt theo `title` (error code) để quyết định UX (vd `Reservation.WindowExpired` → ẩn nút Seat, chuyển sang luồng vãng lai). `detail` dùng làm text toast mặc định.

---

## 5. Sơ đồ luồng màn hình

```
┌─────────────────────────────────────────────────────────────────┐
│  CASHIER MAIN (Floor Plan realtime — GET /api/cashier/floor-plan) │
│   Bàn có reservation đang HOLDING → badge "đã đặt HH:MM"          │
│                                   ┌──────────────────┐            │
│                                   │  📅 ĐẶT BÀN       │ ── nút module
│                                   └────────┬─────────┘            │
└────────────────────────────────────────────┼────────────────────┘
                                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  SC-A · DANH SÁCH RESERVATION (GET /api/reservations)             │
│  Lọc: [Ngày] [Trạng thái]                    [+ Tạo đặt bàn]      │
│  ● 18:30 Anh Long·6kh·Bàn 03,04  [HOLDING]  ← highlight          │
│  ○ 19:00 Chị Mai ·2kh·Bàn 07     [BOOKED/PENDING]                │
│  ✗ 12:00 Anh Hùng·4kh·Bàn 02     [NOT_ARRIVED] ← mờ, ko Seat     │
└──────────┬──────────────────────────────────┬───────────────────┘
           │ [+ Tạo]                           │ (chọn dòng BOOKED+HOLDING)
           ▼                                   ▼
┌──────────────────────────────┐   ┌────────────────────────────────┐
│ SC-B · TẠO ĐẶT BÀN            │   │ SC-C · CHI TIẾT (Seat / Hủy)     │
│ 1) Chọn GIỜ trước (bắt buộc)  │   │  Hiện thông tin + nút:           │
│ 2) GET /api/reservations/     │   │   [ Seat ]  (ẩn nếu ko BOOKED   │
│    projection → sơ đồ bàn      │   │     hoặc đã quá window)          │
│    "chiếu tới giờ đó":         │   │   [ Hủy ]                        │
│    bàn overlap = ⚠ ko chọn đc │   └───────┬───────────────┬─────────┘
│ 3) tick nhiều bàn + nhập khách │           │ [Seat]        │ [Hủy]
│ 4) POST /api/reservations      │           ▼               ▼
└──────────────────────────────┘   ┌──────────────┐  POST .../cancel
                                    │ SC-D · SEAT   │  (chọn lý do)
                                    │ floor-plan    │
                                    │ realtime:     │
                                    │ - bàn đã đặt  │
                                    │   tick sẵn    │
                                    │ - đổi đc bàn  │
                                    │ → LOCK từng   │
                                    │   bàn chọn    │
                                    │ → POST .../seat│
                                    └──────────────┘
```

**Lưu ý 2 ngữ cảnh sơ đồ bàn khác nhau:**
- **SC-B (tạo):** sơ đồ **"chiếu tới giờ hẹn"** — `GET /api/reservations/projection` (trạng thái bàn TẠI giờ định đặt).
- **SC-D (seat):** sơ đồ **realtime "lúc này"** — dùng `GET /api/cashier/floor-plan` thông thường (trạng thái bàn hiện tại).

---

## 6. Chi tiết từng API

### 6.1. SC-A — Danh sách reservation (UC-R2)

```
GET /api/reservations?counterId={int}&date={YYYY-MM-DD}&status={optional}
Permission: reservation:view
```

**Query params:**
| Param | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `counterId` | int | ✅ | Quầy đang thao tác |
| `date` | date (`YYYY-MM-DD`) | ✅ | Lọc theo ngày của `targetTime` (mặc định FE để "hôm nay") |
| `status` | string | ❌ | Nếu truyền, lọc đúng status (`BOOKED`/`ARRIVED`/`CANCELLED`/`NOT_ARRIVED`) |

**Response 200:**
```json
{
  "isSuccess": true,
  "data": {
    "items": [
      {
        "reservationId": 12,
        "code": "R-2026-12",
        "customerName": "Anh Long",
        "customerPhone": "0901234567",
        "guestCount": 6,
        "targetTime": "2026-06-28T18:30:00Z",
        "status": "BOOKED",
        "phase": "HOLDING",
        "tableIds": [3, 4]
      }
    ]
  }
}
```

**Hành vi quan trọng:**
- Danh sách **đã sort theo `targetTime` tăng dần**.
- **Lazy-expire:** mỗi lần gọi, BE tự flip các `BOOKED` đã quá `window_end` thành `NOT_ARRIVED`. Nghĩa là FE chỉ cần poll lại list → no-show tự xuất hiện đúng trạng thái. Không cần FE làm gì.
- `phase` chỉ có giá trị khi `status == "BOOKED"` (ngược lại `null`).
- `tableIds` = các bàn **đã đặt** (booking intent), để hiển thị "Bàn 03, 04".

**Gợi ý UI:**
- `phase == "HOLDING"` → highlight dòng (khách sắp/đang tới). Hiện nút Seat.
- `phase == "PENDING"` → dòng bình thường.
- `status ∈ {NOT_ARRIVED, CANCELLED}` → hiển thị mờ, **không** có nút Seat.
- `status == "ARRIVED"` → đã vào bàn; có thể hiển thị link tới phiếu.

**Polling:** màn này nên poll định kỳ (vd 10–15s) hoặc theo cơ chế version `FLOOR_PLAN` của hệ thống để cập nhật no-show + reservation mới.

---

### 6.2. SC-B bước 2 — Sơ đồ bàn "chiếu tới giờ hẹn" (UC-R5)

```
GET /api/reservations/projection?counterId={int}&targetTime={ISO-8601}
Permission: reservation:create
```

Dùng **trong màn Tạo**, sau khi nhân viên chọn giờ. Trả về trạng thái mỗi bàn của quầy **tại giờ định đặt**, và những reservation đang phủ lên khung giờ đó.

**Response 200:**
```json
{
  "isSuccess": true,
  "data": {
    "counterId": 1,
    "targetTime": "2026-06-28T18:45:00Z",
    "tables": [
      { "tableId": 3, "tableCode": "B03", "areaId": 2, "areaName": "Tầng 1", "seatCount": 4, "isReservedOverlap": true },
      { "tableId": 5, "tableCode": "B05", "areaId": 2, "areaName": "Tầng 1", "seatCount": 4, "isReservedOverlap": false }
    ],
    "overlappingReservations": [
      {
        "reservationId": 12, "customerName": "Anh Long", "customerPhone": "0901234567",
        "guestCount": 6, "targetTime": "2026-06-28T18:30:00Z", "tableIds": [3, 4]
      }
    ]
  }
}
```

**Hành vi:**
- `isReservedOverlap == true` ⇔ bàn đó **không chọn được** để đặt giờ này, vì có một reservation `BOOKED` khác mà **cửa sổ giữ bàn của nó chồng** với cửa sổ của booking mới (`[targetTime ± buffer]`). Đây là **overlap theo khoảng**, không phải trùng đúng một thời điểm.
- `overlappingReservations` = danh sách reservation đang gây chặn, để FE hiển thị "Bàn này đã có Anh Long đặt 18:30".
- Chỉ reservation `BOOKED` được tính (đã `CANCELLED`/`NOT_ARRIVED`/`ARRIVED` không chặn).

**Gợi ý UI:** vẽ sơ đồ bàn; bàn `isReservedOverlap` để màu cảnh báo + **disable chọn**; còn lại cho tick. Khi đổi giờ ở ô chọn giờ → gọi lại projection.

---

### 6.3. SC-B bước 4 — Tạo reservation (UC-R1)

```
POST /api/reservations
Permission: reservation:create
Content-Type: application/json
```

**Request body:**
```json
{
  "counterId": 1,
  "targetTime": "2026-06-28T18:30:00Z",
  "customerName": "Anh Long",
  "customerPhone": "0901234567",
  "guestCount": 6,
  "note": "sinh nhật, cần ghế cao",
  "tableIds": [3, 4]
}
```
| Field | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `counterId` | int | ✅ | > 0 |
| `targetTime` | datetime ISO-8601 | ✅ | Giờ khách hẹn |
| `customerName` | string | ✅ | ≤ 200 ký tự |
| `customerPhone` | string | ✅ | ≤ 20 ký tự |
| `guestCount` | short | ✅ | > 0 |
| `note` | string | ❌ | ≤ 500 ký tự |
| `tableIds` | int[] | ✅ | ≥ 1 phần tử, mọi bàn cùng `counterId` |

**Response 201:**
```json
{ "isSuccess": true, "data": { "reservationId": 12, "code": "R-2026-12" } }
```
`code` là mã hiển thị (vd "R-2026-12"), BE tự sinh.

**Các lỗi có thể trả:**
| HTTP | error code (`title`) | Khi nào | Gợi ý UX |
|---|---|---|---|
| 400 | (validation) | thiếu field/sai định dạng/`tableIds` rỗng | hiện lỗi tại field |
| 400 | `Reservation.NoTables` | không có bàn nào | "Phải chọn ít nhất một bàn" |
| 404 | `Table.NotFound` | bàn không tồn tại / đã inactive | refresh sơ đồ bàn |
| 409 | `Reservation.TablesCrossCounter` | có bàn khác quầy | "Mọi bàn phải cùng một quầy" |
| 409 | `Reservation.TableOverlap` | có bàn trùng khung giờ với reservation BOOKED khác | "Bàn X đã có người đặt trùng giờ" — gợi ý chọn bàn/giờ khác |

> Note: duplicate `tableIds` (vd `[3,3]`) được BE tự khử trùng → 1 bàn. FE không cần lo.

---

### 6.4. SC-D — Seat: mở phiếu từ reservation (UC-R3)

Đây là luồng **2 bước** (giống mở phiếu thường):

**Bước 1 — Khóa từng bàn sẽ ngồi (bắt buộc trước khi seat).**
```
POST /api/cashier/tables/{tableId}/lock        (giữ lock + heartbeat)
DELETE /api/cashier/tables/{tableId}/lock      (nhả khi rời màn)
```
FE phải acquire lock cho **mọi bàn** mà nhân viên chọn ở SC-D (giống thao tác mở phiếu). Lock là per-bàn; một nhân viên giữ nhiều bàn cùng lúc được. Nếu một bàn đang bị người khác giữ → bước seat sẽ fail với `TableLock.NotHeld`.

**Bước 2 — Gọi seat:**
```
POST /api/reservations/{reservationId}/seat
Permission: reservation:seat
```
```json
{
  "tables": [
    { "tableId": 3, "guestCount": 3 },
    { "tableId": 6, "guestCount": 3 }
  ]
}
```
- `tables` = **bàn thực tế khách ngồi** (có thể khác bàn đã đặt). Ở SC-D, tick sẵn bàn đã đặt nhưng cho phép bỏ/thêm.
- Mỗi phần tử mở **một Ticket riêng** trên bàn đó.

**Response 200:**
```json
{
  "isSuccess": true,
  "data": {
    "tickets": [
      { "ticketId": 101, "code": "TK-20260628-101", "tableId": 3 },
      { "ticketId": 102, "code": "TK-20260628-102", "tableId": 6 }
    ]
  }
}
```
Sau seat: reservation → `ARRIVED`, các bàn → Occupied. FE điều hướng về floor plan / mở phiếu vừa tạo để order tiếp (tiếp tục như luồng dine-in bình thường).

**Các lỗi có thể trả (theo thứ tự BE kiểm tra):**
| HTTP | error code | Khi nào | Gợi ý UX |
|---|---|---|---|
| 404 | `Reservation.NotFound` | reservation không tồn tại | quay lại list |
| 409 | `Reservation.NotBooked` | reservation đã `ARRIVED`/`CANCELLED`/`NOT_ARRIVED` | "Đặt bàn này không còn ở trạng thái chờ" — refresh list |
| 409 | `Reservation.WindowExpired` | quá `window_end` (no-show) | **ẩn nút Seat**, hướng dẫn mở phiếu vãng lai bình thường |
| 409 | `TableLock.NotHeld` | chưa khóa được bàn / bàn bị người khác giữ | "Bàn X đang được người khác thao tác" |
| 404 | `Table.NotFound` | bàn chọn không tồn tại/inactive | refresh sơ đồ |
| 409 | `Reservation.SeatTablesCrossCounter` | bàn chọn không thuộc quầy của reservation | chỉ cho chọn bàn cùng quầy |
| 409 | `Ticket.NoOpenCashDrawer` | quầy chưa mở ca tiền mặt | "Hãy mở ca trước khi seat" |

> Toàn bộ seat là **atomic**: nếu một bàn fail (vd thiếu lock), **không** phiếu nào được mở. FE chỉ cần xử lý lỗi rồi cho thử lại.

---

### 6.5. Hủy reservation (UC-R4)

```
POST /api/reservations/{reservationId}/cancel
Permission: reservation:cancel
```
```json
{ "cancellationReasonId": 5, "note": "khách gọi báo hủy" }
```
| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `cancellationReasonId` | int | ✅ | Chọn từ danh mục Cancellation Reason (lookup chung của hệ thống) |
| `note` | string | ❌ | Ghi chú thêm |

**Response: 204 No Content** (không body).

**Lỗi:**
| HTTP | error code | Khi nào |
|---|---|---|
| 404 | `Reservation.NotFound` | không tồn tại |
| 409 | `Reservation.NotBooked` | đã `ARRIVED`/`CANCELLED`/`NOT_ARRIVED` (chỉ hủy được khi đang `BOOKED`) |
| 404 | `CancellationReason.NotFound` | lý do không tồn tại/inactive |

> Cancel chỉ dành cho trường hợp khách **gọi báo hủy trước giờ**. Trường hợp khách không đến (quá giờ) thì **không cần hủy tay** — hệ thống tự để `NOT_ARRIVED`.

---

### 6.6. Cảnh báo walk-in trên bàn đã đặt (BR-R2)

Không có API riêng. Khi nhân viên mở phiếu vãng lai trên bàn (luồng `POST /api/cashier/tickets` thông thường), FE lấy thông tin từ **floor plan**:

```
GET /api/cashier/floor-plan?counterId={int}
```
Trong response, mỗi bàn có field `upcomingReservation` (≠ null khi bàn đang trong cửa sổ giữ bàn HOLDING):
```json
{
  "tableId": 3, "status": "AVAILABLE", "openTicketCount": 0,
  "upcomingReservation": {
    "reservationId": 12, "customerName": "Anh Long", "customerPhone": "0901234567",
    "guestCount": 6, "targetTime": "2026-06-28T18:30:00Z", "status": "BOOKED"
  }
}
```
**Gợi ý UI:** bàn có `upcomingReservation` → vẽ badge "đã đặt HH:MM". Khi nhân viên bấm mở phiếu vãng lai lên bàn đó → hiện **modal cảnh báo** ("Bàn này có đặt trước lúc 18:30 — Anh Long") nhưng **vẫn cho phép** tiếp tục. Đây là cảnh báo, không phải chặn.

---

## 7. Bảng tổng hợp: trạng thái → hành động cho phép (UI matrix)

| Reservation status / phase | Hiện trong list | Nút Seat | Nút Hủy | Ghi chú |
|---|---|---|---|---|
| `BOOKED` + `PENDING` | ✅ (bình thường) | ✅ (cho seat sớm được) | ✅ | chưa tới giờ |
| `BOOKED` + `HOLDING` | ✅ (**highlight**) | ✅ | ✅ | khách sắp/đang tới |
| `BOOKED` + `EXPIRED` | ✅ (thoáng qua) | ⚠️ sẽ fail `WindowExpired` | ✅ | lần load sau sẽ thành NOT_ARRIVED |
| `NOT_ARRIVED` | ✅ (mờ) | ❌ ẩn | ❌ ẩn | no-show, xử như vãng lai |
| `ARRIVED` | ✅ (mờ/link phiếu) | ❌ ẩn | ❌ ẩn | đã vào bàn |
| `CANCELLED` | ✅ (mờ) | ❌ ẩn | ❌ ẩn | đã hủy |

> Mẹo an toàn: kể cả khi FE ẩn nút theo trạng thái cũ, BE vẫn là nguồn chân lý — nếu user thao tác trên dữ liệu cũ (race), API sẽ trả `Reservation.NotBooked`/`WindowExpired`; FE bắt lỗi đó để refresh list.

---

## 8. Checklist cho FE

- [ ] Nút "Đặt bàn" trên màn Cashier chính → mở module SC-A.
- [ ] SC-A: gọi list theo `counterId` + `date`; poll lại định kỳ; tô màu theo `status`/`phase`; ẩn Seat/Hủy theo bảng §7.
- [ ] SC-B: **ép chọn giờ trước**, rồi mới gọi `projection`; disable bàn `isReservedOverlap`; overlay `overlappingReservations`; submit create; bắt các lỗi §6.3.
- [ ] SC-D: dùng floor plan realtime, tick sẵn bàn đã đặt, cho đổi; **acquire lock từng bàn** trước khi seat; nhả lock khi rời màn; bắt lỗi §6.4 (đặc biệt `WindowExpired` → chuyển sang vãng lai).
- [ ] Hủy: chọn lý do từ lookup; bắt `NotBooked`.
- [ ] Floor plan thường: vẽ badge "đã đặt" từ `upcomingReservation`; modal cảnh báo khi mở phiếu vãng lai lên bàn đó (vẫn cho phép).
- [ ] Bắt lỗi theo `title` (error code), hiển thị `detail` làm toast mặc định; 403 → ẩn nút theo permission.

---

## 9. Tham chiếu nhanh — tất cả endpoint

| Method | Route | Permission | Mục đích |
|---|---|---|---|
| GET | `/api/reservations?counterId&date&status` | `reservation:view` | Danh sách (UC-R2, lazy-expire) |
| GET | `/api/reservations/projection?counterId&targetTime` | `reservation:create` | Sơ đồ bàn chiếu tới giờ (UC-R5) |
| POST | `/api/reservations` | `reservation:create` | Tạo reservation (UC-R1) |
| POST | `/api/reservations/{id}/seat` | `reservation:seat` | Seat → mở phiếu (UC-R3) |
| POST | `/api/reservations/{id}/cancel` | `reservation:cancel` | Hủy (UC-R4) |
| POST/DELETE | `/api/cashier/tables/{tableId}/lock` | (lock bàn) | Giữ/nhả lock trước khi seat |
| GET | `/api/cashier/floor-plan?counterId` | `floor_plan:view_cashier` | Floor plan realtime + badge `upcomingReservation` |

---

*Mọi field/enum/error code trong tài liệu này lấy trực tiếp từ code backend (`src/Rpom.Application/Reservation/*`, `src/Rpom.Api/Endpoints/Cashier/Reservations/*`). Nếu thấy sai lệch khi tích hợp, báo backend để đồng bộ.*
