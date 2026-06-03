using Microsoft.EntityFrameworkCore;
using Rpom.Domain.Common;
using Rpom.Infrastructure.Database;

namespace Rpom.Infrastructure.Outbox;

internal sealed class IdempotentDomainEventHandler<TDomainEvent>(
    IDomainEventHandler<TDomainEvent> decorated,
    ApplicationDbContext dbContext)
    : DomainEventHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public override async Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var outboxMessageConsumer = new OutboxMessageConsumer(domainEvent.Id, decorated.GetType().Name);

        if (await OutboxConsumerExistsAsync(outboxMessageConsumer)) return;

        await decorated.Handle(domainEvent, cancellationToken);

        await InsertOutboxConsumerAsync(outboxMessageConsumer);
    }

    private async Task<bool> OutboxConsumerExistsAsync(OutboxMessageConsumer outboxMessageConsumer)
    {
        return await dbContext.OutboxMessageConsumers
            .AnyAsync(c =>
                c.OutboxMessageId == outboxMessageConsumer.OutboxMessageId &&
                c.Name == outboxMessageConsumer.Name);
    }

    private async Task InsertOutboxConsumerAsync(OutboxMessageConsumer outboxMessageConsumer)
    {
        dbContext.OutboxMessageConsumers.Add(outboxMessageConsumer);
        await dbContext.SaveChangesAsync();
    }
}
