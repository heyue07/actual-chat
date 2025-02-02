using ActualChat.Contacts;
using ActualChat.Hosting;
using ActualChat.Invite;
using ActualChat.UI.Blazor.Services;
using ActualChat.Users;

namespace ActualChat.Chat.UI.Blazor.Services;

public record ChatHub(IServiceProvider Services, Session Session) : IHasServices, IServiceProvider
{
    private IChats? _chats;
    private IRoles? _roles;
    private IAuthors? _authors;
    private IReactions? _reactions;
    private IInvites? _invites;
    private IContacts? _contacts;
    private IAvatars? _avatars;
    private IAccounts? _accounts;
    private AccountSettings? _accountSettings;
    private ChatUI? _chatUI;
    private ActiveChatsUI? _activeChatsUI;
    private AuthorUI? _authorUI;
    private AccountUI? _accountUI;
    private SelectionUI? _selectionUI;
    private ChatEditorUI? _chatEditorUI;
    private ChatListUI? _chatListUI;
    private ClipboardUI? _clipboardUI;
    private PanelsUI? _panelsUI;
    private ShareUI? _shareUI;
    private ModalUI? _modalUI;
    private ToastUI? _toastUI;
    private BannerUI? _bannerUI;
    private LanguageUI? _languageUI;
    private FeedbackUI? _feedbackUI;
    private TuneUI? _tuneUI;
    private UICommander? _uiCommander;
    private UIEventHub? _uiEventHub;
    private History? _history;
    private Features? _features;
    private BrowserInfo? _browserInfo;
    private TimeZoneConverter? _timeZoneConverter;
    private UrlMapper? _urlMapper;
    private NavigationManager? _nav;
    private Dispatcher? _dispatcher;
    private MomentClockSet? _clocks;
    private IStateFactory? _stateFactory;
    private KeyedFactory<IChatMarkupHub, ChatId>? _chatMarkupHubFactory;
    private BlazorCircuitContext? _circuitContext;
    private HostInfo? _hostInfo;
    private IJSRuntime? _js;

    public IChats Chats => _chats ??= Services.GetRequiredService<IChats>();
    public IAuthors Authors => _authors ??= Services.GetRequiredService<IAuthors>();
    public IReactions Reactions => _reactions ??= Services.GetRequiredService<IReactions>();
    public IRoles Roles => _roles ??= Services.GetRequiredService<IRoles>();
    public IInvites Invites => _invites ??= Services.GetRequiredService<IInvites>();
    public IContacts Contacts => _contacts ??= Services.GetRequiredService<IContacts>();
    public IAvatars Avatars => _avatars ??= Services.GetRequiredService<IAvatars>();
    public IAccounts Accounts => _accounts ??= Services.GetRequiredService<IAccounts>();
    public AccountSettings AccountSettings => _accountSettings ??= Services.AccountSettings();
    public ChatUI ChatUI => _chatUI ??= Services.GetRequiredService<ChatUI>();
    public ActiveChatsUI ActiveChatsUI => _activeChatsUI ??= Services.GetRequiredService<ActiveChatsUI>();
    public AuthorUI AuthorUI => _authorUI ??= Services.GetRequiredService<AuthorUI>();
    public AccountUI AccountUI => _accountUI ??= Services.GetRequiredService<AccountUI>();
    public SelectionUI SelectionUI => _selectionUI ??= Services.GetRequiredService<SelectionUI>();
    public ChatEditorUI ChatEditorUI => _chatEditorUI ??= Services.GetRequiredService<ChatEditorUI>();
    public ChatListUI ChatListUI => _chatListUI ??= Services.GetRequiredService<ChatListUI>();
    public ClipboardUI ClipboardUI => _clipboardUI ??= Services.GetRequiredService<ClipboardUI>();
    public PanelsUI PanelsUI => _panelsUI ??= Services.GetRequiredService<PanelsUI>();
    public ShareUI ShareUI => _shareUI ??= Services.GetRequiredService<ShareUI>();
    public ModalUI ModalUI => _modalUI ??= Services.GetRequiredService<ModalUI>();
    public ToastUI ToastUI => _toastUI ??= Services.GetRequiredService<ToastUI>();
    public BannerUI BannerUI => _bannerUI ??= Services.GetRequiredService<BannerUI>();
    public LanguageUI LanguageUI => _languageUI ??= Services.GetRequiredService<LanguageUI>();
    public FeedbackUI FeedbackUI => _feedbackUI ??= Services.GetRequiredService<FeedbackUI>();
    public TuneUI TuneUI => _tuneUI ??= Services.GetRequiredService<TuneUI>();
    public UICommander UICommander => _uiCommander ??= Services.UICommander();
    public UIEventHub UIEventHub => _uiEventHub ??= Services.UIEventHub();
    public History History => _history ??= Services.GetRequiredService<History>();
    public Features Features => _features ??= Services.GetRequiredService<Features>();
    public BrowserInfo BrowserInfo => _browserInfo ??= Services.GetRequiredService<BrowserInfo>();
    public TimeZoneConverter TimeZoneConverter => _timeZoneConverter ??= Services.GetRequiredService<TimeZoneConverter>();
    public UrlMapper UrlMapper => _urlMapper ??= Services.UrlMapper();
    public NavigationManager Nav => _nav ??= Services.GetRequiredService<NavigationManager>();
    public Dispatcher Dispatcher => _dispatcher ??= Services.GetRequiredService<Dispatcher>();
    public MomentClockSet Clocks => _clocks ??= Services.Clocks();
    public IStateFactory StateFactory => _stateFactory ??= Services.StateFactory();
    public KeyedFactory<IChatMarkupHub, ChatId> ChatMarkupHubFactory
        => _chatMarkupHubFactory ??= Services.GetRequiredService<KeyedFactory<IChatMarkupHub, ChatId>>();
    public BlazorCircuitContext CircuitContext => _circuitContext ??= Services.GetRequiredService<BlazorCircuitContext>();
    public HostInfo HostInfo => _hostInfo ??= Services.GetRequiredService<HostInfo>();
    public IJSRuntime JS => _js ??= Services.JSRuntime();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetService(Type serviceType)
        => Services.GetService(serviceType);

    // This record relies on referential equality
    public virtual bool Equals(ChatHub? other)
        => ReferenceEquals(this, other);
    public override int GetHashCode()
        => RuntimeHelpers.GetHashCode(this);
}
