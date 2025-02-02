using System.Diagnostics.CodeAnalysis;
using ActualChat.Db.Module;
using ActualChat.Hosting;
using ActualChat.Media.Db;
using ActualChat.Redis.Module;
using Stl.Fusion.EntityFramework.Operations;

namespace ActualChat.Media.Module;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public sealed class MediaServiceModule : HostModule<MediaSettings>
{
    public MediaServiceModule(IServiceProvider moduleServices) : base(moduleServices) { }

    protected override void InjectServices(IServiceCollection services)
    {
        base.InjectServices(services);
        if (!HostInfo.AppKind.IsServer())
            return; // Server-side only module

        // Redis
        var redisModule = Host.GetModule<RedisModule>();
        redisModule.AddRedisDb<MediaDbContext>(services, Settings.Redis);

        // DB
        var dbModule = Host.GetModule<DbModule>();
        services.AddSingleton<IDbInitializer, MediaDbInitializer>();
        dbModule.AddDbContextServices<MediaDbContext>(services, Settings.Db, db => {
            db.AddEntityResolver<string, DbMedia>();
        });

        // Commander & Fusion
        var commander = services.AddCommander();
        commander.AddHandlerFilter((handler, commandType) => {
            // 1. Check if this is DbOperationScopeProvider<MediaDbContext> handler
            if (handler is not InterfaceCommandHandler<ICommand> ich)
                return true;
            if (ich.ServiceType != typeof(DbOperationScopeProvider<MediaDbContext>))
                return true;
            // 2. Make sure it's intact only for local commands
            var commandAssembly = commandType.Assembly;
            if (commandAssembly == typeof(IMediaBackend).Assembly) // Media.Contracts assembly
                return true;
            return false;
        });

        var fusion = services.AddFusion();

        // Module's own services
        fusion.AddService<IMediaBackend, MediaBackend>();

        // Controllers, etc.
        services.AddMvcCore().AddApplicationPart(GetType().Assembly);
    }
}
