using System.Diagnostics.CodeAnalysis;
using ActualChat.Hosting;
using ActualChat.UI.Blazor.App.Services;
using Stl.Plugins;

namespace ActualChat.UI.Blazor.App.Module;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class BlazorUIAppModule : HostModule, IBlazorUIModule
{
    public static string ImportName => "blazorApp";

    public BlazorUIAppModule(IPluginInfoProvider.Query _) : base(_) { }
    [ServiceConstructor]
    public BlazorUIAppModule(IPluginHost plugins) : base(plugins) { }

    public override void InjectServices(IServiceCollection services)
    {
        if (!HostInfo.AppKind.HasBlazorUI())
            return; // Blazor UI only module

        services.AddScoped<SignOutReloader>(c => new SignOutReloader(c));
        services.ConfigureUILifetimeEvents(events => {
            events.OnAppInitialized += c => {
                var signOutReloader = c.GetRequiredService<SignOutReloader>();
                signOutReloader.Start();
            };
        });

        var fusion = services.AddFusion();
        fusion.AddComputeService<AppPresenceReporter>(ServiceLifetime.Scoped);
        services.AddSingleton(_ => new AppPresenceReporter.Options {
            AwayTimeout = Constants.Presence.AwayTimeout,
        });
    }
}
