using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Domain.Sales;

namespace Rpom.Application.ShiftSessions.Shared;

public sealed record CashCountSummary(
    int DenominationId,
    decimal FaceValue,
    int Quantity,
    decimal Subtotal);

/// <summary>
/// Cross-feature DTO shared by Login (auto-resume), OpenShiftSession,
/// CloseShiftSession, GetCurrentShiftSession.
/// </summary>
public sealed record ShiftSessionSummary(
    long Id,
    int ShiftId,
    int? CounterId,
    int? KitchenStationId,
    bool HasCashTracking,
    string Status,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal? OpeningCash,
    decimal? ExpectedClosingCash,
    decimal? ActualClosingCash,
    decimal? Variance,
    IReadOnlyList<CashCountSummary>? OpeningCashCounts,
    IReadOnlyList<CashCountSummary>? ClosingCashCounts);

public static class ShiftSessionMapper
{
    /// <summary>
    /// Query the staff's current OPEN session (with cash counts + denomination
    /// face values joined). Returns null if no open session exists.
    /// </summary>
    public static async Task<ShiftSessionSummary?> LoadCurrentForStaffAsync(
        IDbContext dbContext, int staffAccountId, CancellationToken ct)
    {
        var session = await dbContext.ShiftSessions
            .Include(x => x.CashCounts).ThenInclude(c => c.Denomination)
            .FirstOrDefaultAsync(
                x => x.StaffAccountId == staffAccountId
                     && x.Status == ShiftSessionStatus.Open,
                ct);

        return session is null ? null : Map(session);
    }

    public static ShiftSessionSummary Map(ShiftSession s) =>
        new(
            Id: s.Id,
            ShiftId: s.ShiftId,
            CounterId: s.CounterId,
            KitchenStationId: s.KitchenStationId,
            HasCashTracking: s.HasCashTracking,
            Status: s.Status,
            OpenedAt: s.OpenedAt,
            ClosedAt: s.ClosedAt,
            OpeningCash: s.OpeningCash,
            ExpectedClosingCash: s.ExpectedClosingCash,
            ActualClosingCash: s.ActualClosingCash,
            Variance: s.Variance,
            OpeningCashCounts: ToList(s, ShiftSessionCashPhase.Opening),
            ClosingCashCounts: ToList(s, ShiftSessionCashPhase.Closing));

    private static IReadOnlyList<CashCountSummary>? ToList(ShiftSession s, string phase)
    {
        if (!s.HasCashTracking) return null;
        var rows = s.CashCounts
            .Where(c => c.Phase == phase)
            .Select(c => new CashCountSummary(
                DenominationId: c.DenominationId,
                FaceValue: c.Denomination?.FaceValue ?? 0,
                Quantity: c.Quantity,
                Subtotal: c.Subtotal))
            .ToList();
        return rows.Count == 0 ? null : rows;
    }
}
