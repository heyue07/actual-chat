using System.Collections.Concurrent;
using ActualChat.Commands;

namespace ActualChat.Core.UnitTests.Commands;

public class ScheduledCommandTestService
{
    public readonly ConcurrentQueue<IEvent> ProcessedEvents = new ();

    [CommandHandler]
    public virtual Task ProcessTestCommand(TestCommand command, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return Task.CompletedTask;

        new TestEvent(command.Error).EnqueueOnCompletion(Queues.Default);
        return Task.CompletedTask;
    }

    [CommandHandler]
    public virtual Task ProcessTestCommand2(TestCommand2 command, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return Task.CompletedTask;

        new TestEvent2().EnqueueOnCompletion(Queues.Default);
        return Task.CompletedTask;
    }

    [EventHandler]
    public virtual async Task ProcessTestEvent(TestEvent @event, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return;

        if (@event.Error != null)
            throw new InvalidOperationException(@event.Error);

        await Task.Delay(250, cancellationToken);
        ProcessedEvents.Enqueue(@event);
    }

    [EventHandler]
    public virtual async Task ProcessTestEvent2(TestEvent2 @event, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return;

        await Task.Delay(250, cancellationToken);
        ProcessedEvents.Enqueue(@event);
    }
}
