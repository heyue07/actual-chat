using System.Diagnostics.CodeAnalysis;
using ActualChat.Audio;
using ActualChat.Audio.UI.Blazor.Components;
using ActualChat.Chat.UI.Blazor.Components.MarkupParts;
using ActualChat.Chat.UI.Blazor.Components.MarkupParts.CodeBlockMarkupView;
using ActualChat.Chat.UI.Blazor.Components.NewChat;
using ActualChat.Chat.UI.Blazor.Components.Settings;
using ActualChat.Chat.UI.Blazor.Services;
using ActualChat.Chat.UI.Blazor.Testing;
using ActualChat.Hosting;
using ActualChat.UI.Blazor.Events;
using ActualChat.UI.Blazor.Services;

namespace ActualChat.Chat.UI.Blazor.Module;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class ChatBlazorUIModule : HostModule, IBlazorUIModule
{
    public static string ImportName => "chat";

    public ChatBlazorUIModule(IServiceProvider services) : base(services) { }

    protected override void InjectServices(IServiceCollection services)
    {
        if (!HostInfo.AppKind.HasBlazorUI())
            return; // Blazor UI only module

        var fusion = services.AddFusion();

        // Singletons
        fusion.AddService<VirtualListTestService>();

        // Scoped / Blazor Circuit services
        services.AddScoped(_ => new NavbarUI());
        services.AddScoped(c => new PanelsUI(c));
        services.AddScoped(c => new AuthorUI(c));
        services.AddScoped<IAudioOutputController>(c => new AudioOutputController(c));
        services.AddScoped(c => new CachingKeyedFactory<IChatMarkupHub, ChatId, ChatMarkupHub>(c, 256).ToGeneric());

        // Chat UI
        fusion.AddService<ChatUI>(ServiceLifetime.Scoped);
        fusion.AddService<ChatListUI>(ServiceLifetime.Scoped);
        fusion.AddService<ChatAudioUI>(ServiceLifetime.Scoped);
        fusion.AddService<ChatEditorUI>(ServiceLifetime.Scoped);
        fusion.AddService<ChatPlayers>(ServiceLifetime.Scoped);
        services.AddScoped(_ => new PlayableTextPaletteProvider());
        services.AddScoped(c => new ActiveChatsUI(c));

        // Chat activity
        services.AddScoped(c => new ChatActivity(c));
        fusion.AddService<ChatRecordingActivity>(ServiceLifetime.Transient);

        // Settings
        services.AddSingleton(_ => new AudioSettings());
        services.AddScoped(c => new LanguageUI(c));
        services.AddScoped(c => new OnboardingUI(c));

        // IMarkupViews
        services.AddTypeMapper<IMarkupView>(map => map
            .Add<NewLineMarkup, NewLineMarkupView>()
            .Add<UrlMarkup, UrlMarkupView>()
            .Add<MentionMarkup, MentionView>()
            .Add<PreformattedTextMarkup, PreformattedTextMarkupView>()
            .Add<PlayableTextMarkup, PlayableTextMarkupView>()
            .Add<CodeBlockMarkup, CodeBlockMarkupView>()
            .Add<StylizedMarkup, StylizedMarkupView>()
            .Add<PlainTextMarkup, PlainTextMarkupView>()
            .Add<UnparsedTextMarkup, PlainTextMarkupView>()
            .Add<MarkupSeq, MarkupSeqView>()
            .Add<Markup, MarkupView>()
        );
        // IModalViews
        services.AddTypeMap<IModalView>(map => map
            .Add<AvatarSelectModal.Model, AvatarSelectModal>()
            .Add<NoSecondaryLanguageModal.Model, NoSecondaryLanguageModal>()
            .Add<ChatSettingsModal.Model, ChatSettingsModal>()
            .Add<InviteAuthorModal.Model, InviteAuthorModal>()
            .Add<NewChatModal.Model, NewChatModal>()
            .Add<OnboardingModal.Model, OnboardingModal>()
            .Add<SettingsModal.Model, SettingsModal>()
            .Add<AuthorModal.Model, AuthorModal>()
            .Add<DeleteMessageModal.Model, DeleteMessageModal>()
            .Add<LeaveChatConfirmationModal.Model, LeaveChatConfirmationModal>()
        );
        // IBannerViews
        services.AddTypeMap<IBannerView>(map => map
            .Add<SwitchToWasmBanner.Model, SwitchToWasmBanner>()
        );

        services.ConfigureUIEvents(
            eventHub => eventHub.Subscribe<ShowSettingsEvent>((@event, ct) => {
                var modalUI = eventHub.Services.GetRequiredService<ModalUI>();
                _ = modalUI.Show(SettingsModal.Model.Instance, true);
                return Task.CompletedTask;
            }));
    }
}
