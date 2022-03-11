using ActualChat.Chat.UI.Blazor.Services;
using ActualChat.Chat.UI.Blazor.Testing;
using ActualChat.Hosting;
using ActualChat.MediaPlayback;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stl.Plugins;

namespace ActualChat.Chat.UI.Blazor.Module;

public class ChatBlazorUIModule : HostModule, IBlazorUIModule
{
    public static string ImportName => "chat";

    public ChatBlazorUIModule(IPluginInfoProvider.Query _) : base(_) { }

    [ServiceConstructor]
    public ChatBlazorUIModule(IPluginHost plugins) : base(plugins) { }

    public override void InjectServices(IServiceCollection services)
    {
        if (!HostInfo.RequiredServiceScopes.Contains(ServiceScope.BlazorUI))
            return; // Blazor UI only module

        var fusion = services.AddFusion();

        // Singletons
        services.TryAddSingleton<IChatMediaResolver, BuiltInChatMediaResolver>();
        fusion.AddComputeService<VirtualListTestService>();
        fusion.AddComputeService<ChatActivities>(ServiceLifetime.Scoped);

        // Scoped / Blazor Circuit services
        services.TryAddScoped<ChatController>();
        services.TryAddScoped<IChatPlayerFactory, ChatPlayerFactory>();
        services.TryAddScoped<ChatActivities>();

        services.RegisterNavItems<ChatLinks>();
    }
}
