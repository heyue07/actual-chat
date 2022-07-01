namespace ActualChat.UI.Blazor.Layouts;

public interface IBaseLayoutBackend
{
    [JSInvokable]
    public Task NavigateTo(string url);
}
