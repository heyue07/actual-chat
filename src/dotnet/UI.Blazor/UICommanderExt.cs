using Stl.CommandR.Internal;

namespace ActualChat.UI.Blazor;

public static class UICommanderExt
{
    public static Task RunNothing(this UICommander uiCommander)
    {
        var command = new LocalActionCommand() { Handler = static _ => Task.CompletedTask };
        return uiCommander.Run(command, CancellationToken.None);
    }

    public static async Task RunLocal(
        this UICommander uiCommander,
        Func<CancellationToken, Task> commandTaskFactory,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(15);
        using var cts = new CancellationTokenSource(timeout.GetValueOrDefault().Positive());
        await uiCommander.RunLocal(commandTaskFactory, cts.Token).ConfigureAwait(false);
    }

    public static Task RunLocal(
        this UICommander uiCommander,
        Func<CancellationToken, Task> commandTaskFactory,
        CancellationToken cancellationToken)
    {
        var command = new LocalActionCommand() { Handler = _ => commandTaskFactory.Invoke(cancellationToken) };
        return uiCommander.Run(command, CancellationToken.None);
    }

    public static void ShowError(this UICommander uiCommander, Exception error)
    {
        var command = new LocalActionCommand() { Handler = _ => throw error };
        _ = uiCommander.Run(command, CancellationToken.None);
    }
}
