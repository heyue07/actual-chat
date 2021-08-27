using ActualChat.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stl.DependencyInjection;
using Stl.Fusion;
using Stl.Fusion.Client;
using Stl.Plugins;

namespace ActualChat.Distribution.Client.Module
{
    public class DistributionClientModule : HostModule
    {
        public DistributionClientModule(IPluginInfoProvider.Query _) : base(_) { }
        [ServiceConstructor]
        public DistributionClientModule(IPluginHost plugins) : base(plugins) { }

        public override void InjectServices(IServiceCollection services)
        {
            if (!HostInfo.RequiredServiceScopes.Contains(ServiceScope.Client))
                return; // Client-side only module

            
            // var fusionClient = services.AddFusion().AddRestEaseClient();
            // fusionClient.AddReplicaService<ITodoService, ITodoClientDef>();
        }
    }
}
