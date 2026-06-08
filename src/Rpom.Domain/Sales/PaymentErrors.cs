using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;
public static class PaymentErrors
{
    public static readonly Error TicketNotFound = Error.NotFound(
        "Payment.TicketNotFound",
        "Không tìm thấy hoá đơn này.");

    public static readonly Error TicketNotOpen = Error.Conflict(
        "Payment.TicketNotOpen",
        "Hoá đơn đã đóng hoặc đã huỷ — không thể tạo thanh toán.");

    public static readonly Error AmountNotPositive = Error.BadRequest(
        "Payment.AmountNotPositive",
        "Số tiền thanh toán phải lớn hơn 0.");

    public static readonly Error NothingToPay = Error.Conflict(
        "Payment.NothingToPay",
        "Hoá đơn đã được thanh toán đủ — không thể tạo thêm thanh toán.");

    public static readonly Error AmountExceedsRemaining = Error.Conflict(
        "Payment.AmountExceedsRemaining",
        "Số tiền thanh toán vượt quá số tiền còn lại cần thanh toán của hoá đơn.");

    public static readonly Error PaymentMethodMissing = Error.Failure(
        "Payment.PaymentMethodMissing",
        "Phương thức thanh toán chưa được cấu hình trong hệ thống.");

    public static readonly Error PaymentNotFound = Error.NotFound(
        "Payment.PaymentNotFound",
        "Không tìm thấy giao dịch thanh toán này.");

    public static readonly Error PaymentNotPending = Error.Conflict(
        "Payment.PaymentNotPending",
        "Giao dịch thanh toán không ở trạng thái chờ — không thể thao tác.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "Payment.ConcurrencyConflict",
        "Hoá đơn vừa được cập nhật bởi thao tác khác — vui lòng tải lại và thử lại.");

    public static readonly Error QrGatewayUnavailable = Error.Failure(
        "Payment.QrGatewayUnavailable",
        "Cổng thanh toán QR (SePay) chưa được cấu hình.");

    public static readonly Error WebhookUnauthorized = Error.Unauthorized(
        "Payment.WebhookUnauthorized",
        "Yêu cầu webhook không hợp lệ — sai API key.");

    public static readonly Error PendingPaymentExists = Error.Conflict(
    "Payment.PendingPaymentExists",
    "Hóa đơn đang có một giao dịch chờ thanh toán. Vui lòng hoàn tất hoặc hủy giao dịch đó trước.");
}
