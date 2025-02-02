using ActualChat.Kvas;

namespace ActualChat.UI.Blazor.Services;

public class RightPanel
{
    private const string StatePrefix = nameof(RightPanel) + "UI";
    private readonly IStoredState<bool> _isVisible;

    private IServiceProvider Services => Owner.Services;
    private History History => Owner.History;
    private Dispatcher Dispatcher => Owner.Dispatcher;

    public PanelsUI Owner { get; }
    // ReSharper disable once InconsistentlySynchronizedField
    public IState<bool> IsVisible => _isVisible;

    public RightPanel(PanelsUI owner)
    {
        Owner = owner;
        var stateFactory = Services.StateFactory();
        var localSettings = Services.GetRequiredService<LocalSettings>().WithPrefix(StatePrefix);
        _isVisible = stateFactory.NewKvasStored<bool>(
            new (localSettings, nameof(IsVisible)) {
                InitialValue = false,
                Corrector = (isVisible, _) => new ValueTask<bool>(isVisible && Owner.IsWide()),
                Category = StateCategories.Get(GetType(), nameof(IsVisible)),
            });
        var initialState = new OwnHistoryState(this, false);
        History.Register(initialState);
        _ = _isVisible.WhenRead.ContinueWith(_1 => {
            var isVisible = _isVisible.Value;
            if (initialState.IsVisible != isVisible)
                // Handles special case:
                // Right panel is open in wide view after loading.
                // Open modal.
                // Close modal.
                // Closing modal should invoke GoBack navigation but adds a new state instead.
                // To workaround this issue we should correct default state for right panel.
                _ = History.Dispatcher.InvokeAsync(
                    () => History.FixDefaultState(initialState, new OwnHistoryState(this, isVisible)));
        }, TaskScheduler.Default);
    }

    public void Toggle()
        => SetIsVisible(!IsVisible.Value);

    public void SetIsVisible(bool value)
        => _ = Dispatcher.InvokeAsync(() => {
            if (_isVisible.Value == value)
                return;

            _isVisible.Value = value;
            History.Save<OwnHistoryState>();
        });

    // Nested types

    private sealed record OwnHistoryState(RightPanel Host, bool IsVisible) : HistoryState
    {
        public override int BackStepCount => IsVisible ? 1 : 0;

        public override string Format()
            => IsVisible.ToString();

        public override HistoryState Save()
            => With(Host.IsVisible.Value);

        public override void Apply(HistoryTransition transition)
            => Host.SetIsVisible(IsVisible);

        public override HistoryState? Back()
            => BackStepCount == 0 ? null : With(!IsVisible);

        // "With" helpers

        public OwnHistoryState With(bool isVisible)
            => IsVisible == isVisible ? this : this with { IsVisible = isVisible };
    }
}
