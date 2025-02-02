using ActualChat.Commands;
using ActualChat.Commands.Internal;
using ActualChat.Testing.Collections;

namespace ActualChat.Core.UnitTests.Commands;

[Collection(nameof(AppHostTests)), Trait("Category", nameof(AppHostTests))]
public class ScheduledCommandsTest: TestBase
{
    public ScheduledCommandsTest(ITestOutputHelper @out) : base(@out)
    { }

    [Fact]
    public async Task EnqueueEventOnCommandCompletion()
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddLocalCommandQueues()
            .AddCommandQueueScheduler()
            .AddFusion()
            .AddService<ScheduledCommandTestService>()
            .Services
            .BuildServiceProvider();
        await services.HostedServices().Start();

        var queue = (LocalCommandQueue)services.GetRequiredService<ICommandQueues>()[default];
        var testService = services.GetRequiredService<ScheduledCommandTestService>();
        var commander = services.GetRequiredService<ICommander>();

        testService.ProcessedEvents.Count.Should().Be(0);
        await commander.Call(new TestCommand(null));
        testService.ProcessedEvents.Count.Should().Be(0);

        await Awaiter.WaitFor(() => queue.SuccessCount != 0);

        testService.ProcessedEvents.Count.Should().Be(1);
    }

    [Fact]
    public async Task MultipleEventHandlersAreCalled()
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddLocalCommandQueues()
            .AddCommandQueueScheduler()
            .AddSingleton<DedicatedInterfaceEventHandler>()
            .AddCommander(c => c.AddHandlers<DedicatedInterfaceEventHandler>())
            .AddFusion()
            .AddService<ScheduledCommandTestService>()
            .AddService<DedicatedEventHandler>()
            .Services
            .BuildServiceProvider();
        await services.HostedServices().Start();

        var queue = (LocalCommandQueue)services.GetRequiredService<ICommandQueues>()[default];
        var testService = services.GetRequiredService<ScheduledCommandTestService>();
        var commander = services.GetRequiredService<ICommander>();

        testService.ProcessedEvents.Count.Should().Be(0);
        await commander.Call(new TestCommand2());

        await Awaiter.WaitFor(() => queue.SuccessCount == 2);

        foreach (var eventCommand in testService.ProcessedEvents)
            Out.WriteLine(eventCommand.ToString());

        testService.ProcessedEvents.Count.Should().Be(3);
    }

    [Fact]
    public async Task MultipleQueuesDontLeadToDuplicateEvents()
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddLocalCommandQueues()
            .AddCommandQueueScheduler()
            .AddSingleton<DedicatedInterfaceEventHandler>()
            .AddCommander(c => c.AddHandlers<DedicatedInterfaceEventHandler>())
            .AddFusion()
            .AddService<ScheduledCommandTestService>()
            .AddService<DedicatedEventHandler>()
            .Services
            .BuildServiceProvider();
        await services.HostedServices().Start();

        var queue = (LocalCommandQueue)services.GetRequiredService<ICommandQueues>()[default];
        var testService = services.GetRequiredService<ScheduledCommandTestService>();
        var commander = services.GetRequiredService<ICommander>();

        testService.ProcessedEvents.Count.Should().Be(0);
        await commander.Call(new TestCommand3());

        await Awaiter.WaitFor(() => queue.SuccessCount == 2);

        foreach (var eventCommand in testService.ProcessedEvents)
            Out.WriteLine(eventCommand.ToString());

        testService.ProcessedEvents.Count.Should().Be(3);
    }
}
