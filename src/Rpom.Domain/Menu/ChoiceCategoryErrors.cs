using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

public static class ChoiceCategoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ChoiceCategory.NotFound",
        "Không tìm thấy Nhóm lựa chọn.");

    public static readonly Error NameDuplicate = Error.Conflict(
        "ChoiceCategory.NameDuplicate",
        "Tên Nhóm lựa chọn đã tồn tại.");

    public static readonly Error InUse = Error.Conflict(
        "ChoiceCategory.InUse",
        "Không xoá được vì Nhóm lựa chọn đang được Set menu sử dụng.");

    public static readonly Error ItemNotFound = Error.NotFound(
        "ChoiceCategory.ItemNotFound",
        "Một hoặc nhiều Mặt hàng (modifier) không tồn tại.");

    public static readonly Error DuplicateModifierItem = Error.BadRequest(
        "ChoiceCategory.DuplicateModifierItem",
        "Danh sách modifier có Mặt hàng trùng lặp.");

    public static Error MaxPerModifierExceedsMaxChoice(int itemId, int maxChoice) => Error.BadRequest(
        "Modifier.MaxPerModifierExceedsMaxChoice",
        $"MaxPerModifier của Item {itemId} vượt quá MaxChoice ({maxChoice}) của Nhóm lựa chọn. " +
        "Modifier không thể được chọn đủ số lượng vì bị giới hạn bởi MaxChoice.");

    public static Error MinPerModifierExceedsMaxPerModifier(int itemId) => Error.BadRequest(
        "Modifier.MinPerModifierExceedsMaxPerModifier",
        $"MinPerModifier của Item {itemId} lớn hơn MaxPerModifier. Min phải ≤ Max.");

    public static Error MinPerModifierSumExceedsMaxChoice(int sumMin, int maxChoice) => Error.BadRequest(
        "Modifier.MinPerModifierSumExceedsMaxChoice",
        $"Tổng MinPerModifier ({sumMin}) của tất cả modifier vượt quá MaxChoice ({maxChoice}) của Nhóm lựa chọn. " +
        "Không thể thoả mãn tất cả yêu cầu tối thiểu của modifier.");
}
