using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Cashier.SplitTicketPreview;

/// <summary>
///     Dry-run của <see cref="SplitTicket.SplitTicket" />: chạy y hệt logic tách + recompute
///     trong một transaction rồi ROLLBACK để KHÔNG ghi DB. Trả về tổng tiền hai phiếu sau khi
///     tách, phục vụ FE preview real-time khi cashier chỉnh số lượng. Vì tái dùng handler thật
///     nên số preview == số commit tuyệt đối (không có công thức tính song song).
/// </summary>
public static class SplitTicketPreview
{
    public sealed record SplitItemInput(long OrderItemId, decimal Quantity);

    public sealed record Query(
        long SourceTicketId,
        long? DestinationTicketId,
        int? DestinationTableId,
        short? GuestCount,
        IReadOnlyList<SplitItemInput> Items) : IQuery<Response>;

    public sealed record Response(
        long SourceTotalAmount,
        long DestinationTotalAmount,
        int MovedItemCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.SourceTicketId).GreaterThan(0);
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(i =>
            {
                i.RuleFor(x => x.OrderItemId).GreaterThan(0);
                i.RuleFor(x => x.Quantity).GreaterThan(0);
            });
        }
    }

    internal sealed class Handler(
        IDbContext db,
        IRequestHandler<SplitTicket.SplitTicket.Command, Result<SplitTicket.SplitTicket.Response>> splitHandler)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var command = new SplitTicket.SplitTicket.Command(
                request.SourceTicketId,
                request.DestinationTicketId,
                request.DestinationTableId,
                request.GuestCount,
                request.Items
                    .Select(i => new SplitTicket.SplitTicket.SplitItemInput(i.OrderItemId, i.Quantity))
                    .ToList());

            // Npgsql retry-on-failure: transaction thủ công bắt buộc chạy trong execution strategy.
            IExecutionStrategy strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                // Mỗi lần retry chạy lại lambda trên cùng DbContext. Rollback chỉ revert DB chứ
                // không revert change tracker → clear để lần thử sau không dính tracked state cũ.
                db.ChangeTracker.Clear();

                await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    // Tái dùng handler split THẬT (gọi trực tiếp, không qua MediatR pipeline để
                    // tránh auto-commit của TransactionPipelineBehavior).
                    Result<SplitTicket.SplitTicket.Response> result = await splitHandler.Handle(command, ct);

                    // Dry-run: KHÔNG BAO GIỜ commit. Rollback revert toàn bộ mutation nằm trong
                    // transaction này (move món, tạo phiếu đích mới, recompute, AuditLog, version
                    // bump — tất cả đều trên cùng connection nên cùng bị rollback).
                    // Lưu ý: sequence Ticket.Id (mode mở phiếu mới) bị tiêu hao vì Postgres sequence
                    // không rollback — chỉ tạo lỗ hổng id, không ảnh hưởng đúng/sai con số.
                    await tx.RollbackAsync(ct);

                    return result.IsFailure
                        ? Result.Failure<Response>(result.Error)
                        : Result.Success(new Response(
                            result.Value.SourceTotalAmount,
                            result.Value.DestinationTotalAmount,
                            result.Value.MovedItemCount));
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });
        }
    }
}
