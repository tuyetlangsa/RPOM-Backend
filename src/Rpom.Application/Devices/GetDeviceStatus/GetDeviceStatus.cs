using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Configuration;
using Rpom.Domain.Common;

namespace Rpom.Application.Devices.GetDeviceStatus;

/// <summary>
///     Màn giám sát thiết bị cho Owner/Manager: liệt kê máy POS + màn hình khách kèm
///     <c>LastSeenAt</c> và cờ <c>IsOnline</c> (LastSeenAt trong ngưỡng config). Display heartbeat
///     mỗi lần poll; terminal heartbeat khi tạo QR. Quyền <c>master_data:view</c>.
/// </summary>
public static class GetDeviceStatus
{
    public sealed record Query(int? CounterId) : IQuery<Response>;

    public sealed record Response(
        int OnlineThresholdSeconds,
        IReadOnlyList<TerminalStatus> Terminals,
        IReadOnlyList<DisplayStatus> Displays);

    public sealed record TerminalStatus(
        int Id, int CounterId, string CounterName, string Name,
        bool IsActive, bool HasDisplay, DateTime? LastSeenAt, bool IsOnline);

    public sealed record DisplayStatus(
        int Id, int PosTerminalId, string PosTerminalName, int CounterId, string CounterName, string Name,
        bool IsActive, bool IsActivated, DateTime? LastSeenAt, bool IsOnline);

    internal sealed class Handler(IDbContext db, IDateTimeProvider clock, IConfigValueService config)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            int threshold = await config.GetIntAsync(ConfigCodes.DeviceOnlineThresholdSeconds, 120, ct);
            DateTime now = clock.UtcNow;
            bool Online(DateTime? last) => last is { } l && (now - l).TotalSeconds <= threshold;

            var terminals = await db.PosTerminals
                .Where(t => request.CounterId == null || t.CounterId == request.CounterId)
                .OrderBy(t => t.CounterId).ThenBy(t => t.Name)
                .Select(t => new
                {
                    t.Id, t.CounterId, CounterName = t.Counter.Name, t.Name, t.IsActive, t.LastSeenAt,
                    HasDisplay = db.CustomerDisplays.Any(d => d.PosTerminalId == t.Id && d.IsActive)
                })
                .ToListAsync(ct);

            var displays = await db.CustomerDisplays
                .Where(d => request.CounterId == null || d.PosTerminal.CounterId == request.CounterId)
                .OrderBy(d => d.PosTerminal.CounterId).ThenBy(d => d.Name)
                .Select(d => new
                {
                    d.Id, d.PosTerminalId, PosTerminalName = d.PosTerminal.Name,
                    d.PosTerminal.CounterId, CounterName = d.PosTerminal.Counter.Name, d.Name,
                    d.IsActive, IsActivated = d.ActivatedClientId != null, d.LastSeenAt
                })
                .ToListAsync(ct);

            var terminalDtos = terminals.Select(t => new TerminalStatus(
                t.Id, t.CounterId, t.CounterName, t.Name, t.IsActive, t.HasDisplay,
                t.LastSeenAt, Online(t.LastSeenAt))).ToList();

            var displayDtos = displays.Select(d => new DisplayStatus(
                d.Id, d.PosTerminalId, d.PosTerminalName, d.CounterId, d.CounterName, d.Name,
                d.IsActive, d.IsActivated, d.LastSeenAt, Online(d.LastSeenAt))).ToList();

            return Result.Success(new Response(threshold, terminalDtos, displayDtos));
        }
    }
}
