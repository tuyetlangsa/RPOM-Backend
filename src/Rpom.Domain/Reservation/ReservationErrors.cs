using Rpom.Domain.Common;

namespace Rpom.Domain.Reservation;

public static class ReservationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Reservation.NotFound", "Đặt bàn không tồn tại.");

    public static readonly Error NotBooked = Error.Conflict(
        "Reservation.NotBooked", "Đặt bàn không ở trạng thái BOOKED.");

    public static readonly Error NoTables = Error.BadRequest(
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
