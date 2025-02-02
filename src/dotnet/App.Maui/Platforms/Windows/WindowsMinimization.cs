using Microsoft.Maui.Platform;

namespace ActualChat.App.Maui;

internal static class WindowsMinimization
{
    public static void Configure(Microsoft.UI.Xaml.Window window)
    {
        WinUI.App.AppInstanceActivated += arguments => {
            window.DispatcherQueue.TryEnqueue(() => {
                if (arguments.Contains(JumpListManager.QuitArgs))
                    App.Current.Quit();
                else
                    window.Activate();
            });
        };

        _ = JumpListManager.PopulateJumpList();
        window.Closed += (_, _) => {
            var t = Task.Run(JumpListManager.ClearJumpList);
#pragma warning disable VSTHRD002
            _ = t.Wait(TimeSpan.FromSeconds(5));
#pragma warning restore VSTHRD002
        };

        var appWindow = window.GetAppWindow()!;
        appWindow.Closing += (_, e) => {
            if (!App.MustMinimizeOnQuit)
                return;

            var presenter = (Microsoft.UI.Windowing.OverlappedPresenter)appWindow.Presenter;
            presenter.Minimize();
            e.Cancel = true;
        };
    }
}
