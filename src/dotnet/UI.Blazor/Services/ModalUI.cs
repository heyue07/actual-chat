using Blazored.Modal;
using Blazored.Modal.Services;
using Stl.Extensibility;

namespace ActualChat.UI.Blazor.Services;

public sealed class ModalUI
{
    private ModalService ModalService { get; }
    private IMatchingTypeFinder MatchingTypeFinder { get; }

    public ModalUI(ModalService modalService, IMatchingTypeFinder matchingTypeFinder)
    {
        ModalService = modalService;
        MatchingTypeFinder = matchingTypeFinder;
    }

    public IModalReference Show<TModel>(TModel model, string cls = "")
        where TModel : class
    {
        var componentType = MatchingTypeFinder.TryFind(model.GetType(), typeof(IModalView));
        if (componentType == null)
            throw StandardError.NotFound<IModalView>(
                $"No modal view component for '{model.GetType()}' model.");

        var modalOptions = new ModalOptions {
            Class = $"blazored-modal modal {cls}"
        };
        var modalContent = new RenderFragment(builder => {
            builder.OpenComponent(0, componentType);
            builder.AddAttribute(1, nameof(IModalView<TModel>.ModalModel), model);
            builder.CloseComponent();
        });
        return ModalService.Show(modalContent, modalOptions);
    }
}
