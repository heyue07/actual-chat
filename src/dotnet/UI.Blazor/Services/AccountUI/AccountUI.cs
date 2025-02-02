using ActualChat.Hosting;
using ActualChat.Users;
using Stl.Interception;

namespace ActualChat.UI.Blazor.Services;

public partial class AccountUI : WorkerBase, IComputeService, INotifyInitialized, IHasServices
{
    private readonly TaskCompletionSource _whenLoadedSource = TaskCompletionSourceExt.New();
    private readonly IMutableState<AccountFull> _ownAccount;
    private readonly IMutableState<Moment> _lastChangedAt;
    private readonly TimeSpan _maxInvalidationDelay;
    private AppBlazorCircuitContext? _blazorCircuitContext;
    private IClientAuth? _clientAuth;
    private ILogger? _log;

    private AppBlazorCircuitContext BlazorCircuitContext =>
        _blazorCircuitContext ??= Services.GetRequiredService<AppBlazorCircuitContext>();
    private ILogger Log => _log ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; }
    public HostInfo HostInfo { get; }
    public Session Session { get; }
    public IAccounts Accounts { get; }
    public IClientAuth ClientAuth => _clientAuth ??= Services.GetRequiredService<IClientAuth>();
    public IMomentClock Clock { get; }

    public Task WhenLoaded => _whenLoadedSource.Task;
    public IState<AccountFull> OwnAccount => _ownAccount;
    public IState<Moment> LastChangedAt => _lastChangedAt;
    public Moment StartedAt { get; }
    public event Action<AccountFull>? Changed;

    public AccountUI(IServiceProvider services)
    {
        Services = services;
        HostInfo = services.GetRequiredService<HostInfo>();
        Session = services.Session();
        Accounts = services.GetRequiredService<IAccounts>();
        Clock = services.Clocks().CpuClock;

        StartedAt = Clock.Now;
        _maxInvalidationDelay = TimeSpan.FromSeconds(HostInfo.AppKind.IsServer() ? 0.5 : 2);
        var ownAccountComputed = Computed.GetExisting(() => Accounts.GetOwn(Session, default));
        var ownAccount = ownAccountComputed?.IsConsistent() == true &&  ownAccountComputed.HasValue ? ownAccountComputed.Value : null;
        var initialOwnAccount = ownAccount ?? AccountFull.Loading;

        var stateFactory = services.StateFactory();
        _ownAccount = stateFactory.NewMutable<AccountFull>(new () {
            InitialValue = initialOwnAccount,
            Category = StateCategories.Get(GetType(), nameof(OwnAccount)),
        });
        _lastChangedAt = stateFactory.NewMutable<Moment>(new () {
            InitialValue = StartedAt,
            Category = StateCategories.Get(GetType(), nameof(OwnAccount)),
        });
        if (!ReferenceEquals(initialOwnAccount, AccountFull.Loading))
            _whenLoadedSource.TrySetResult();
    }

    void INotifyInitialized.Initialized()
        => this.Start();

    public TimeSpan GetPostChangeInvalidationDelay()
        => GetPostChangeInvalidationDelay(TimeSpan.FromSeconds(2));
    public TimeSpan GetPostChangeInvalidationDelay(TimeSpan maxInvalidationDelay)
    {
        maxInvalidationDelay = maxInvalidationDelay.Clamp(default, _maxInvalidationDelay);
        var changedAt = Moment.Max(LastChangedAt.Value, StartedAt + TimeSpan.FromSeconds(1));
        return (changedAt + maxInvalidationDelay - Clock.Now).Positive();
    }
}
