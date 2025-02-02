using System.Diagnostics.CodeAnalysis;
using ActualChat.Chat.Db;
using ActualChat.Db;
using ActualChat.Db.Module;
using ActualChat.Hosting;
using ActualChat.Redis.Module;
using ActualChat.Users.Events;
using Microsoft.EntityFrameworkCore;
using Stl.Fusion.EntityFramework.Operations;

namespace ActualChat.Chat.Module;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public sealed class ChatServiceModule : HostModule<ChatSettings>
{
    public ChatServiceModule(IServiceProvider moduleServices) : base(moduleServices) { }

    protected override void InjectServices(IServiceCollection services)
    {
        base.InjectServices(services);
        if (!HostInfo.AppKind.IsServer())
            return; // Server-side only module

        // Redis
        var redisModule = Host.GetModule<RedisModule>();
        redisModule.AddRedisDb<ChatDbContext>(services, Settings.Redis);

        // DB
        var dbModule = Host.GetModule<DbModule>();
        services.AddSingleton<IDbInitializer, ChatDbInitializer>();
        dbModule.AddDbContextServices<ChatDbContext>(services, Settings.Db, db => {
            // DbChat
            db.AddEntityResolver<string, DbChat>();

            // DbChatEntry
            db.AddShardLocalIdGenerator<ChatDbContext, DbChatEntry, DbChatEntryShardRef>(
                dbContext => dbContext.ChatEntries,
                (e, shardKey) => e.ChatId == shardKey.ChatId && e.Kind == shardKey.Kind,
                e => e.LocalId);

            // DbAuthor
            db.AddShardLocalIdGenerator(dbContext => dbContext.Authors,
                (e, shardKey) => e.ChatId == shardKey, e => e.LocalId);
            db.AddEntityResolver<string, DbAuthor>(_ => new() {
                QueryTransformer = query => query.Include(a => a.Roles),
            });

            // DbRole
            db.AddShardLocalIdGenerator(dbContext => dbContext.Roles,
                (e, shardKey) => e.ChatId == shardKey, e => e.LocalId);
            db.AddEntityResolver<string, DbRole>();
        });

        // Commander & Fusion
        var commander = services.AddCommander();
        commander.AddHandlerFilter((handler, commandType) => {
            // 1. Check if this is DbOperationScopeProvider<AudioDbContext> handler
            if (handler is not InterfaceCommandHandler<ICommand> ich)
                return true;
            if (ich.ServiceType != typeof(DbOperationScopeProvider<ChatDbContext>))
                return true;

            // 2. Make sure it's intact only for local commands
            var commandAssembly = commandType.Assembly;
            return commandAssembly == typeof(ChatModule).Assembly // Chat assembly
                || commandAssembly == typeof(IAuthors).Assembly // Chat.Contracts assembly
                || commandAssembly == typeof(Authors).Assembly // Chat.Service assembly
                || commandType == typeof(NewUserEvent); // NewUserEvent is handled by Chat service - TODO(AK): abstraction leak!!
        });
        var fusion = services.AddFusion();

        // Chats
        fusion.AddService<IChats, Chats>();
        fusion.AddService<IChatsBackend, ChatsBackend>();
        commander.AddService<IChatsUpgradeBackend, ChatsUpgradeBackend>();

        // Authors
        fusion.AddService<IAuthors, Authors>();
        fusion.AddService<IAuthorsBackend, AuthorsBackend>();
        commander.AddService<IAuthorsUpgradeBackend, AuthorsUpgradeBackend>();

        // Roles
        fusion.AddService<IRoles, Roles>();
        fusion.AddService<IRolesBackend, RolesBackend>();

        // Mentions
        fusion.AddService<IMentions, Mentions>();
        fusion.AddService<IMentionsBackend, MentionsBackend>();

        // Reactions
        fusion.AddService<IReactions, Reactions>();
        fusion.AddService<IReactionsBackend, ReactionsBackend>();

        // ContentSaver
        services.AddResponseCaching();
        commander.AddService<IContentSaverBackend, ContentSaverBackend>();
        services.AddSingleton<IUploadProcessor, ImageUploadProcessor>();
        services.AddSingleton<IUploadProcessor, VideoUploadProcessor>();

        // ChatMarkupHub
        services.AddSingleton(c =>
            new CachingKeyedFactory<IBackendChatMarkupHub, ChatId, BackendChatMarkupHub>(c, 4096, true).ToGeneric());

        // Controllers, etc.
        services.AddMvcCore().AddApplicationPart(GetType().Assembly);
    }
}
