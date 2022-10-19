using ActualChat.Commands;
using ActualChat.Testing.Collections;
using Stl.Time.Testing;

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
            .AddFusion()
            .AddLocalCommandScheduler()
            .AddComputeService<ScheduledCommandTestService>()
            .Services
            .BuildServiceProvider();
        await services.HostedServices().Start();

        var testService = services.GetRequiredService<ScheduledCommandTestService>();
        var commander = services.GetRequiredService<ICommander>();

        testService.ProcessedEvents.Count.Should().Be(0);
        var commandTask = commander.Call(new TestCommand(null));
        testService.ProcessedEvents.Count.Should().Be(0);

        await commandTask.ConfigureAwait(false);
        var testClock = new TestClock();
        await testClock.Delay(1000).ConfigureAwait(false);

        testService.ProcessedEvents.Count.Should().Be(1);
    }

    [Fact]
    public async Task MultipleEventHandlersAreCalled()
    {
        await using var services = new ServiceCollection()
            .AddCommander()
            .AddLocalEventHandlers()
            .AddHandlers<DedicatedInterfaceEventHandler>()
            .Services
            .AddSingleton<DedicatedInterfaceEventHandler>()
            .AddFusion()
            .AddLocalCommandScheduler()
            .AddComputeService<ScheduledCommandTestService>()
            .AddComputeService<DedicatedEventHandler>()
            .Services
            .BuildServiceProvider();
        await services.HostedServices().Start();

        var testService = services.GetRequiredService<ScheduledCommandTestService>();
        var commander = services.GetRequiredService<ICommander>();

        testService.ProcessedEvents.Count.Should().Be(0);
        var commandTask = commander.Call(new TestCommand2());
        testService.ProcessedEvents.Count.Should().Be(0);

        await commandTask.ConfigureAwait(false);
        var testClock = new TestClock();
        await testClock.Delay(2000).ConfigureAwait(false);

        testService.ProcessedEvents.Count.Should().Be(2);
    }
}
