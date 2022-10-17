namespace ActualChat.UI.Blazor.Services;

public static class ScreenSizeExt
{
    public static bool IsUnknown(this ScreenSize screenSize)
        => screenSize is ScreenSize.Unknown;

    public static bool IsMobile(this ScreenSize screenSize)
        => screenSize is ScreenSize.Small;

    public static bool IsDesktop(this ScreenSize screenSize)
        => !screenSize.IsMobile();
}
