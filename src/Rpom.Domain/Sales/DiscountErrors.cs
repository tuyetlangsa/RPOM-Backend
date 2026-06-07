using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

public static class DiscountErrors
{
    public static readonly Error PolicyNotFound =
        Error.NotFound("Discount.PolicyNotFound", "Discount policy not found or inactive.");

    public static readonly Error NotApplicable =
        Error.Conflict("Discount.NotApplicable", "No policy condition matches the current ticket.");

    public static readonly Error DaysOfWeekMismatch =
        Error.Conflict("Discount.DaysOfWeekMismatch", "Policy is not active on today's day of week.");

    public static readonly Error AlreadyApplied =
        Error.Conflict("Discount.AlreadyApplied", "Another discount policy is already applied. Remove it first.");
}
