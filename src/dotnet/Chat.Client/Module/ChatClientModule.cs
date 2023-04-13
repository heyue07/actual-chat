using System.Diagnostics.CodeAnalysis;
using System.Net;
using ActualChat.Hosting;
using Stl.Fusion.Client;

namespace ActualChat.Chat.Module;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public sealed class ChatClientModule : HostModule
{
    [ServiceConstructor]
    public ChatClientModule(IServiceProvider services) : base(services) { }

    protected override void InjectServices(IServiceCollection services)
    {
        if (!HostInfo.AppKind.IsClient())
            return; // Client-side only module

        var fusionClient = services.AddFusion().AddRestEaseClient();
        fusionClient.ConfigureHttpClient((c, name, o) => {
            o.HttpClientActions.Add(client => {
                client.DefaultRequestVersion = HttpVersion.Version30;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            });
        });
        fusionClient.AddReplicaService<IChats, IChatsClientDef>();
        fusionClient.AddReplicaService<IAuthors, IAuthorsClientDef>();
        fusionClient.AddReplicaService<IRoles, IRolesClientDef>();
        fusionClient.AddReplicaService<IMentions, IMentionsClientDef>();
        fusionClient.AddReplicaService<IReactions, IReactionsClientDef>();
    }
}
