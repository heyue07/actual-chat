using ActualChat.Chat;
namespace ActualChat.UI.Blazor.Services;

public class VisualMediaViewerUI(IServiceProvider services)
{
    private ModalUI ModalUI { get; } = services.GetRequiredService<ModalUI>();

    public async Task Show(
        string url,
        string? altText = null,
        ChatEntry? chatEntry = null)
    {
        var model = new VisualMediaViewerModal.Model(url, altText, chatEntry);
        var modalRef = await ModalUI.Show(model).ConfigureAwait(false);
        await modalRef.WhenClosed.ConfigureAwait(false);
    }
}
