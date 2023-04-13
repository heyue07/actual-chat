using System.Diagnostics.CodeAnalysis;
using ActualChat.Audio;
using ActualChat.Chat.UI.Blazor.Components.Settings;
using ActualChat.Chat.UI.Blazor.Services;
using ActualChat.Chat.UI.Blazor.Testing;
using ActualChat.Hosting;
using ActualChat.UI.Blazor.Events;
using ActualChat.UI.Blazor.Services;

namespace ActualChat.Chat.UI.Blazor.Module;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class ChatBlazorUIModule : HostModule, IBlazorUIModule
{
    public static string ImportName => "chat";

    [ServiceConstructor]
    public ChatBlazorUIModule(IServiceProvider services) : base(services) { }

    protected override void InjectServices(IServiceCollection services)
    {
        if (!HostInfo.AppKind.HasBlazorUI())
            return; // Blazor UI only module

        var fusion = services.AddFusion();

        // Singletons
        fusion.AddComputeService<VirtualListTestService>();

        // Scoped / Blazor Circuit services
        services.AddScoped<NavbarUI>(c => new NavbarUI());
        services.AddScoped<PanelsUI>(c => new PanelsUI(c));
        services.AddScoped<AuthorUI>(c => new AuthorUI(c));
        services.AddScoped<IAudioOutputController>(c => new AudioOutputController(c));
        services.AddScoped(c => new CachingKeyedFactory<IChatMarkupHub, ChatId, ChatMarkupHub>(c, 256).ToGeneric());

        // Chat UI
        fusion.AddComputeService<ChatAudioUI>(ServiceLifetime.Scoped);
        fusion.AddComputeService<ChatEditorUI>(ServiceLifetime.Scoped);
        fusion.AddComputeService<ChatUI>(ServiceLifetime.Scoped);
        fusion.AddComputeService<ChatPlayers>(ServiceLifetime.Scoped);
        services.AddScoped<PlayableTextPaletteProvider>(_ => new PlayableTextPaletteProvider());
        services.AddScoped(c => new ActiveChatsUI(c));

        // Chat activity
        services.AddScoped<ChatActivity>(c => new ChatActivity(c));
        fusion.AddComputeService<ChatRecordingActivity>(ServiceLifetime.Transient);

        // Settings
        services.AddSingleton<AudioSettings>(_ => new AudioSettings());
        services.AddScoped<LanguageUI>(c => new LanguageUI(c));
        services.AddScoped<OnboardingUI>(c => new OnboardingUI(c));

        // Matching type finder
        services.AddSingleton<IMatchingTypeRegistry>(c => new ChatBlazorUIMatchingTypeRegistry());

        services.ConfigureUILifetimeEvents(events
            => events.OnCircuitContextCreated += RegisterShowSettingsHandler);
    }

    private void RegisterShowSettingsHandler(IServiceProvider services)
    {
        var eventHub = services.UIEventHub();
        eventHub.Subscribe<ShowSettingsEvent>((@event, ct) => {
            var modalUI = services.GetRequiredService<ModalUI>();
            modalUI.Show(SettingsModal.Model.Instance, true);
            return Task.CompletedTask;
        });
    }
}
