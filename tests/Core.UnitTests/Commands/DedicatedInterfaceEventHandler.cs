namespace ActualChat.Core.UnitTests.Commands;

public class DedicatedInterfaceEventHandler : ICommandHandler<TestEvent2>
{
    private ScheduledCommandTestService TestService { get; }

    public DedicatedInterfaceEventHandler(ScheduledCommandTestService testService)
        => TestService = testService;

    public Task OnCommand(TestEvent2 @event, CommandContext context, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return Task.CompletedTask;

        throw new InvalidOperationException("Should not run!");
 #pragma warning disable CS0162
        TestService.ProcessedEvents.Enqueue(@event);
        return Task.CompletedTask;
 #pragma warning restore CS0162
    }
}
