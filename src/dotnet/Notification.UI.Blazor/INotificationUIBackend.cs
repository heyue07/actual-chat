namespace ActualChat.Notification.UI.Blazor;

public interface INotificationUIBackend
{
    [JSInvokable]
    void HandleNotificationNavigation(string absoluteUrl);

    [JSInvokable]
    Task UpdateNotificationStatus(string permissionState);
}
