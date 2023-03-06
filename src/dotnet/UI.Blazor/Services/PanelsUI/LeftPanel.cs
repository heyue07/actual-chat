using ActualChat.UI.Blazor.Services.Internal;

namespace ActualChat.UI.Blazor.Services;

public class LeftPanel
{
    private readonly IMutableState<bool> _isVisible;
    private readonly object _lock = new();

    private ILogger Log { get; }
    public PanelsUI Owner { get; }
    // ReSharper disable once InconsistentlySynchronizedField
    public IState<bool> IsVisible => _isVisible;
    public event EventHandler? VisibilityChanged;

    public LeftPanel(PanelsUI owner)
    {
        Owner = owner;
        Log = Owner.Services.LogFor(GetType());

        _isVisible = Owner.Services.StateFactory().NewMutable(true);
        Owner.History.Register(new OwnHistoryState(this, true));
    }

    public void SetIsVisible(bool value)
    {
        var localUrl = Owner.History.LocalUrl;
        value |= localUrl.IsChatRoot(); // Always visible if @ /chat
        value &= !localUrl.IsDocsOrDocsRoot(); // Always invisible if @ /docs*
        value |= IsWide(); // Always visible if wide

        bool oldIsVisible;
        lock (_lock) {
            oldIsVisible = _isVisible.Value;
            if (oldIsVisible != value)
                _isVisible.Value = value;
        }
        if (oldIsVisible != value) {
            Log.LogDebug("Visibility changed: {IsVisible}", value);
            Owner.History.Save<OwnHistoryState>();
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool IsWide()
        => Owner.ScreenSize.Value.IsWide();

    // Nested types

    private sealed record OwnHistoryState(LeftPanel Host, bool IsVisible) : HistoryState
    {
        public override int BackStepCount => IsVisible ? 0 : 1;
        public override bool IsUriDependent => true;

        public override string Format()
            => IsVisible.ToString();

        public override HistoryState Save()
            => With(Host.IsVisible.Value);

        public override void Apply(HistoryTransition transition)
        {
            var isHistoryMove = transition.LocationChangeKind is LocationChangeKind.HistoryMove;
            Host.SetIsVisible(IsVisible && isHistoryMove);
        }

        public override HistoryState? Back()
            => BackStepCount == 0 ? null : With(!IsVisible);

        // "With" helpers

        public OwnHistoryState With(bool isVisible)
            => IsVisible == isVisible ? this : this with { IsVisible = isVisible };
    }
}
