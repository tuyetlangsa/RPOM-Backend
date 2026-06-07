using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Quartz;
using Rpom.Application;
using Rpom.Application.Abstraction.Clock;
using Rpom.Domain.Common;
using Rpom.Infrastructure.Database;
using Rpom.Infrastructure.Serialization;

namespace Rpom.Infrastructure.Outbox;

[DisallowConcurrentExecution]
internal sealed class ProcessOutboxJob(
    ApplicationDbContext dbContext,
    IServiceScopeFactory serviceScopeFactory,
    IDateTimeProvider dateTimeProvider,
    IOptions<OutboxOptions> outboxOptions,
    ILogger<ProcessOutboxJob> logger) : IJob
{
    private const string ModuleName = "RPOM";

    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("{Module} - Beginning to process outbox messages", ModuleName);

        IReadOnlyList<OutboxMessageResponse> outboxMessages = await GetOutboxMessagesAsync();

        foreach (OutboxMessageResponse outboxMessage in outboxMessages)
        {
            Exception? exception = null;

            try
            {
                IDomainEvent domainEvent = JsonConvert.DeserializeObject<IDomainEvent>(
                    outboxMessage.Content,
                    SerializerSettings.Instance)!;

                using IServiceScope scope = serviceScopeFactory.CreateScope();

                IEnumerable<IDomainEventHandler> handlers = DomainEventHandlersFactory.GetHandlers(
                    domainEvent.GetType(),
                    scope.ServiceProvider,
                    AssemblyReference.Assembly);

                foreach (IDomainEventHandler domainEventHandler in handlers)
                {
                    await domainEventHandler.Handle(domainEvent, context.CancellationToken);
                }
            }
            catch (Exception caughtException)
            {
                logger.LogError(
                    caughtException,
                    "{Module} - Exception while processing outbox message {MessageId}",
                    ModuleName,
                    outboxMessage.Id);

                exception = caughtException;
            }

            await UpdateOutboxMessageAsync(outboxMessage, exception);
        }

        logger.LogInformation("{Module} - Completed processing outbox messages", ModuleName);
    }

    private async Task<IReadOnlyList<OutboxMessageResponse>> GetOutboxMessagesAsync()
    {
        List<OutboxMessageResponse> messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(outboxOptions.Value.BatchSize)
            .Select(m => new OutboxMessageResponse
            {
                Id = m.Id,
                Content = m.Content
            })
            .ToListAsync();

        return messages;
    }

    private async Task UpdateOutboxMessageAsync(OutboxMessageResponse outboxMessage, Exception? exception)
    {
        OutboxMessage? message = await dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == outboxMessage.Id);

        if (message is not null)
        {
            message.ProcessedOnUtc = dateTimeProvider.UtcNow;
            message.Error = exception?.ToString();
            await dbContext.SaveChangesAsync();
        }
    }

    internal sealed record OutboxMessageResponse
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = null!;
    }
}
