﻿using System.Diagnostics.CodeAnalysis;
using System.Net;
using ActualChat.Blobs.Internal;
using ActualChat.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Stl.Extensibility;
using Stl.Fusion.Client;
using Stl.Fusion.Extensions;
using Stl.Plugins;

namespace ActualChat.Module;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class CoreModule : HostModule<CoreSettings>
{
    public CoreModule(IPluginInfoProvider.Query _) : base(_) { }

    [ServiceConstructor]
    public CoreModule(IPluginHost plugins) : base(plugins) { }

    public override void InjectServices(IServiceCollection services)
    {
        base.InjectServices(services);

        var pluginAssemblies = Plugins.FoundPlugins.InfoByType
            .Select(c => c.Value.Type.TryResolve())
            .Where(c => c != null)
            .Select(c => c!.Assembly)
            .Distinct()
            .ToList();

        // Common services
        services.AddTracer();
        services.AddSingleton<StaticImportsInitializer>();
        services.AddHostedService<StaticImportsInitializer>();
        services.AddSingleton<UrlMapper>(c => new UrlMapper(
            c.GetRequiredService<HostInfo>()));

        // Matching type finder
        services.AddSingleton(new MatchingTypeFinder.Options {
            ScannedAssemblies = pluginAssemblies,
        });
        services.AddSingleton<IMatchingTypeFinder>(c => new MatchingTypeFinder(
            c.GetRequiredService<MatchingTypeFinder.Options>()));

        // DiffEngine
        services.AddSingleton<DiffEngine>(c => new DiffEngine(c));

        // ObjectPoolProvider & PooledValueTaskSourceFactory
        services.AddSingleton<ObjectPoolProvider>(_ => HostInfo.IsDevelopmentInstance
 #pragma warning disable CS0618
            ? new LeakTrackingObjectPoolProvider(new DefaultObjectPoolProvider())
 #pragma warning restore CS0618
            : new DefaultObjectPoolProvider());
        services.AddSingleton(typeof(IValueTaskSourceFactory<>), typeof(PooledValueTaskSourceFactory<>));

        // Fusion
        var fusion = services.AddFusion();
        fusion.AddComputedGraphPruner();
        fusion.AddFusionTime();

        // Features
        services.AddScoped<Features>(c => new Features(c));
        fusion.AddComputeService<IClientFeatures, ClientFeatures>(ServiceLifetime.Scoped);

        if (HostInfo.AppKind.IsServer())
            InjectServerServices(services);
        if (HostInfo.AppKind.IsClient())
            InjectClientServices(services);
    }

    private void InjectServerServices(IServiceCollection services)
    {
        services.AddSingleton<IContentSaver, ContentSaver>();

        var storageBucket = Settings.GoogleStorageBucket;
        if (storageBucket.IsNullOrEmpty()) {
            services.TryAddSingleton<IContentTypeProvider>(sp
                => sp.GetRequiredService<IOptions<StaticFileOptions>>().Value.ContentTypeProvider);
            services.AddSingleton<IBlobStorageProvider>(c => new TempFolderBlobStorageProvider(c));
        }
        else
            services.AddSingleton<IBlobStorageProvider>(new GoogleCloudBlobStorageProvider(storageBucket));

        var fusion = services.AddFusion();
        fusion.AddComputeService<IServerFeatures, ServerFeatures>();
    }

    private void InjectClientServices(IServiceCollection services)
    {
        var fusion = services.AddFusion();
        var fusionClient = fusion.AddRestEaseClient();
        fusionClient.ConfigureHttpClient((c, name, o) => {
            o.HttpClientActions.Add(client => {
                client.DefaultRequestVersion = HttpVersion.Version30;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            });
        });

        // Features
        fusionClient.AddReplicaService<ServerFeaturesClient.IClient, ServerFeaturesClient.IClientDef>();
        fusion.AddComputeService<IServerFeatures, ServerFeaturesClient>();
    }
}
