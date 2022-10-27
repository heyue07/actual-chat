using System.ComponentModel.DataAnnotations;
using ActualChat.Chat.Db;
using ActualChat.Chat.Events;
using ActualChat.Db;
using ActualChat.Hosting;
using ActualChat.Commands;
using ActualChat.Users;
using ActualChat.Users.Events;
using Microsoft.EntityFrameworkCore;
using Stl.Fusion.EntityFramework;
using Stl.Generators;
using Stl.Versioning;

namespace ActualChat.Chat;

public partial class ChatsBackend : DbServiceBase<ChatDbContext>, IChatsBackend
{
    private static readonly TileStack<long> IdTileStack = Constants.Chat.IdTileStack;
    private static readonly string ChatIdAlphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
    private static readonly RandomStringGenerator ChatIdGenerator = new(10, ChatIdAlphabet);

    private IAccountsBackend AccountsBackend { get; }
    private IChatAuthorsBackend ChatAuthorsBackend { get; }
    private IChatRolesBackend ChatRolesBackend { get; }
    private IMarkupParser MarkupParser { get; }
    private IChatMentionResolverFactory ChatMentionResolverFactory { get; }
    private IUserContactsBackend UserContactsBackend { get; }
    private IDbEntityResolver<string, DbChat> DbChatResolver { get; }
    private IDbShardLocalIdGenerator<DbChatEntry, DbChatEntryShardRef> DbChatEntryIdGenerator { get; }
    private DiffEngine DiffEngine { get; }
    private HostInfo HostInfo { get; }

    public ChatsBackend(IServiceProvider services) : base(services)
    {
        AccountsBackend = services.GetRequiredService<IAccountsBackend>();
        ChatAuthorsBackend = services.GetRequiredService<IChatAuthorsBackend>();
        ChatRolesBackend = services.GetRequiredService<IChatRolesBackend>();
        MarkupParser = services.GetRequiredService<IMarkupParser>();
        ChatMentionResolverFactory = services.GetRequiredService<BackendChatMentionResolverFactory>();
        UserContactsBackend = services.GetRequiredService<IUserContactsBackend>();
        DbChatResolver = services.GetRequiredService<IDbEntityResolver<string, DbChat>>();
        DbChatEntryIdGenerator = services.GetRequiredService<IDbShardLocalIdGenerator<DbChatEntry, DbChatEntryShardRef>>();
        DiffEngine = services.GetRequiredService<DiffEngine>();
        HostInfo = services.GetRequiredService<HostInfo>();
    }

    // [ComputeMethod]
    public virtual async Task<Chat?> Get(string chatId, CancellationToken cancellationToken)
    {
        if (chatId.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(chatId);
        var dbChat = await DbChatResolver.Get(chatId, cancellationToken).ConfigureAwait(false);
        return dbChat?.ToModel();
    }

    // [ComputeMethod]
    public virtual async Task<ChatAuthorRules> GetRules(
        string chatId,
        string chatPrincipalId,
        CancellationToken cancellationToken)
    {
        var parsedChatId = new ParsedChatId(chatId);
        if (!parsedChatId.IsValid)
            return ChatAuthorRules.None(chatId);
        var chatPrincipalId = new ChatPrincipalId(chatPrincipalId);
        if (!chatPrincipalId.IsValid)
            return ChatAuthorRules.None(chatId);

        // Peer chat: we don't use actual roles to determine rules here
        var chatType = parsedChatId.Kind.ToChatType();
        if (chatType is ChatType.Peer)
            return await GetPeerChatRules(chatId, chatPrincipalId, cancellationToken).ConfigureAwait(false);

        // Group chat
        var chat = await Get(chatId, cancellationToken).ConfigureAwait(false);
        if (chat == null)
            return ChatAuthorRules.None(chatId);

        var (userId, authorId) = chatPrincipalId;
        ChatAuthorFull? author = null;
        if (!authorId.IsEmpty) {
            author = await ChatAuthorsBackend.Get(chatId, authorId, false, cancellationToken).ConfigureAwait(false);
            userId = author?.UserId ?? Symbol.Empty;
        }
        var account = userId.IsEmpty ? null
            : await AccountsBackend.Get(userId, cancellationToken).ConfigureAwait(false);

        var roles = ImmutableArray<ChatRole>.Empty;
        var isJoined = author is { HasLeft: false };
        if (isJoined) {
            var isAuthenticated = account != null;
            var isAnonymous = author is { IsAnonymous: true };
            roles = await ChatRolesBackend
                .List(chatId, author!.Id, isAuthenticated, isAnonymous, cancellationToken)
                .ConfigureAwait(false);
        }
        var permissions = roles.ToPermissions();
        if (chat.IsPublic) {
            permissions |= ChatPermissions.Join;
            if (!isJoined) {
                var anyoneSystemRole = await ChatRolesBackend.GetSystem(chatId, SystemChatRole.Anyone, cancellationToken).ConfigureAwait(false);
                if (anyoneSystemRole != null && anyoneSystemRole.Permissions.Has(ChatPermissions.SeeMembers))
                    permissions |= ChatPermissions.SeeMembers;
            }
        }
        permissions = permissions.AddImplied();

        var rules = new ChatAuthorRules(chatId, author, account, permissions);
        return rules;
    }

    // [ComputeMethod]
    public virtual async Task<ChatSummary?> GetSummary(
        string chatId,
        CancellationToken cancellationToken)
    {
        var parsedChatId = new ParsedChatId(chatId);
        if (!parsedChatId.IsValid)
            return null;

        var chat = await Get(chatId, cancellationToken).ConfigureAwait(false);
        if (chat == null)
            return null;

        var idRange = await GetIdRange(chatId, ChatEntryType.Text, false, cancellationToken).ConfigureAwait(false);
        var idTile = IdTileStack.FirstLayer.GetTile(idRange.End - 1);
        var tile = await GetTile(chatId, ChatEntryType.Text, idTile.Range, false, cancellationToken).ConfigureAwait(false);
        var lastEntry = tile.Entries.Length > 0 ? tile.Entries[^1] : null;
        return new ChatSummary() {
            TextEntryIdRange = idRange,
            LastTextEntry = lastEntry,
        };
    }

    // [ComputeMethod]
    public virtual async Task<long> GetEntryCount(
        string chatId,
        ChatEntryType entryType,
        Range<long>? idTileRange,
        bool includeRemoved,
        CancellationToken cancellationToken)
    {
        var dbContext = CreateDbContext();
        await using var __ = dbContext.ConfigureAwait(false);

        var dbChatEntries = dbContext.ChatEntries.AsQueryable()
            .Where(e => e.ChatId == chatId && e.Type == entryType);
        if (!includeRemoved)
            dbChatEntries = dbChatEntries.Where(e => !e.IsRemoved);

        if (idTileRange.HasValue) {
            var idRangeValue = idTileRange.GetValueOrDefault();
            IdTileStack.AssertIsTile(idRangeValue);
            dbChatEntries = dbChatEntries
                .Where(e => e.Id >= idRangeValue.Start && e.Id < idRangeValue.End);
        }

        return await dbChatEntries.LongCountAsync(cancellationToken).ConfigureAwait(false);
    }

    // Note that it returns (firstId, lastId + 1) range!
    // [ComputeMethod]
    public virtual async Task<Range<long>> GetIdRange(
        string chatId,
        ChatEntryType entryType,
        bool includeRemoved,
        CancellationToken cancellationToken)
    {
        var minId = await GetMinId(chatId, entryType, cancellationToken).ConfigureAwait(false);

        var dbContext = CreateDbContext();
        await using var _ = dbContext.ConfigureAwait(false);

        var dbChatEntries = dbContext.ChatEntries.AsQueryable()
            .Where(e => e.ChatId == chatId && e.Type == entryType);
        if (!includeRemoved)
            dbChatEntries = dbChatEntries.Where(e => e.IsRemoved == false);
        var maxId = await dbChatEntries
            .OrderByDescending(e => e.Id)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return (minId, Math.Max(minId, maxId) + 1);
    }

    // [ComputeMethod]
    public virtual async Task<ChatTile> GetTile(
        string chatId,
        ChatEntryType entryType,
        Range<long> idTileRange,
        bool includeRemoved,
        CancellationToken cancellationToken)
    {
        var idTile = IdTileStack.GetTile(idTileRange);
        var smallerIdTiles = idTile.Smaller();
        if (smallerIdTiles.Length != 0) {
            var smallerChatTiles = new List<ChatTile>();
            foreach (var smallerIdTile in smallerIdTiles) {
                var smallerChatTile = await GetTile(chatId, entryType, smallerIdTile.Range, includeRemoved, cancellationToken)
                    .ConfigureAwait(false);
                smallerChatTiles.Add(smallerChatTile);
            }
            return new ChatTile(smallerChatTiles, includeRemoved);
        }
        if (!includeRemoved) {
            var fullTile = await GetTile(chatId, entryType, idTileRange, true, cancellationToken).ConfigureAwait(false);
            return new ChatTile(idTileRange, false, fullTile.Entries.Where(e => !e.IsRemoved).ToImmutableArray());
        }

        // If we're here, it's the smallest tile & includeRemoved = true
        var dbContext = CreateDbContext();
        await using var _ = dbContext.ConfigureAwait(false);

        var dbEntries = await dbContext.ChatEntries
            .Where(e => e.ChatId == chatId
                && e.Type == entryType
                && e.Id >= idTile.Range.Start
                && e.Id < idTile.Range.End)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var entryIdsWithAttachments = dbEntries.Where(x => x.HasAttachments)
            .Select(x => x.CompositeId)
            .ToList();
        var allAttachments = entryIdsWithAttachments.Count > 0
            ? await dbContext.TextEntryAttachments
 #pragma warning disable MA0002
                .Where(x => entryIdsWithAttachments.Contains(x.ChatEntryId))
 #pragma warning restore MA0002
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : (IReadOnlyCollection<DbTextEntryAttachment>)Array.Empty<DbTextEntryAttachment>();
        var attachmentsLookup = allAttachments.ToLookup(x => x.ChatEntryId, StringComparer.Ordinal);
        var entries = dbEntries.Select(e => {
                var entryAttachments = attachmentsLookup[e.CompositeId].Select(a => a.ToModel());
                return e.ToModel(entryAttachments);
            })
            .ToImmutableArray();
        return new ChatTile(idTileRange, true, entries);
    }

    // Command handlers

    // [CommandHandler]
    public virtual async Task<Chat?> ChangeChat(
        IChatsBackend.ChangeChatCommand command,
        CancellationToken cancellationToken)
    {
        var (chatId, expectedVersion, change, creatorUserId) = command;
        var context = CommandContext.GetCurrent();
        if (Computed.IsInvalidating()) {
            var invChat = context.Operation().Items.Get<Chat>()!;
            _ = Get(invChat.Id, default);
            return null!;
        }

        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var __ = dbContext.ConfigureAwait(false);

        Chat chat;
        DbChat dbChat;
        if (change.RequireValid().IsCreate(out var update)) {
            chatId = chatId.NullIfEmpty() ?? ChatIdGenerator.Next();
            chat = new Chat() {
                Id = chatId,
                Version = VersionGenerator.NextVersion(),
                CreatedAt = Clocks.SystemClock.Now,
            };
            chat = DiffEngine.Patch(chat, update);
            if (chat.ChatType is not ChatType.Peer && chat.Title.IsNullOrEmpty())
                throw new ValidationException("Chat title cannot be empty.");

            var isPeer = chat.ChatType is ChatType.Peer;
            var parsedChatId = new ParsedChatId(chatId);
            parsedChatId = isPeer ? parsedChatId.AssertPeerFull() : parsedChatId.AssertGroup();

            dbChat = new DbChat(chat);
            dbContext.Add(dbChat);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (chat.ChatType is ChatType.Peer) {
                // Peer chat
                creatorUserId.RequireEmpty("Command.CreatorUserId");
                var (userId1, userId2) = (parsedChatId.UserId1.Id, parsedChatId.UserId2.Id);
                var ownerUserIds = new[] { userId1.Value, userId2.Value };
                await ownerUserIds
                    .Select(userId => ChatAuthorsBackend.GetOrCreate(chatId, userId, true, cancellationToken))
                    .Collect(0)
                    .ConfigureAwait(false);
                var tContact1 = UserContactsBackend.GetOrCreate(userId1, userId2, cancellationToken);
                var tContact2 = UserContactsBackend.GetOrCreate(userId2, userId1, cancellationToken);
                var (contact1, contact2) = await tContact1.Join(tContact2).ConfigureAwait(false);
            }
            else {
                // Group chat
                creatorUserId = creatorUserId.RequireNonEmpty("Command.CreatorUserId");
                var chatAuthor = await ChatAuthorsBackend
                    .GetOrCreate(chatId, creatorUserId, true, cancellationToken)
                    .ConfigureAwait(false);

                var createOwnersRoleCmd = new IChatRolesBackend.ChangeCommand(chatId, "", null, new() {
                    Create = new ChatRoleDiff() {
                        SystemRole = SystemChatRole.Owner,
                        Permissions = ChatPermissions.Owner,
                        AuthorIds = new SetDiff<ImmutableArray<Symbol>, Symbol>() {
                            AddedItems = ImmutableArray<Symbol>.Empty.Add(chatAuthor.Id),
                        },
                    },
                });
                await Commander.Call(createOwnersRoleCmd, cancellationToken).ConfigureAwait(false);

                var createJoinedRoleCmd = new IChatRolesBackend.ChangeCommand(chatId, "", null, new() {
                    Create = new ChatRoleDiff() {
                        SystemRole = SystemChatRole.Anyone,
                        Permissions =
                            ChatPermissions.Write
                            | ChatPermissions.Invite
                            | ChatPermissions.SeeMembers
                            | ChatPermissions.Leave,
                    },
                });
                await Commander.Call(createJoinedRoleCmd, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (change.IsUpdate(out update)) {
            chatId = chatId.RequireNonEmpty("Command.ChatId");
            dbChat = await dbContext.Chats
                .SingleAsync(a => a.Id == chatId, cancellationToken)
                .ConfigureAwait(false);
            chat = dbChat.ToModel();
            if ((update.ChatType ?? chat.ChatType) != chat.ChatType)
                throw StandardError.Constraint("Chat type cannot be changed.");
            VersionChecker.RequireExpected(chat.Version, expectedVersion);

            chat = chat with {
                Version = VersionGenerator.NextVersion(chat.Version),
            };
            chat = DiffEngine.Patch(chat, update);
            if (chat.ChatType is not ChatType.Peer && chat.Title.IsNullOrEmpty())
                throw new ValidationException("Chat title cannot be empty.");
            dbChat.UpdateFrom(chat);
            dbContext.Update(dbChat);
        }
        else
            throw StandardError.NotSupported("Chat removal is not supported yet.");

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        chat = dbChat.ToModel();
        context.Operation().Items.Set(chat);
        return chat;
    }

    [CommandHandler(IsFilter = true, Priority = 1001)] // 1000 = DbOperationScopeProvider, we must "wrap" it
    protected virtual async Task OnChatAuthorCreated(
        IChatAuthorsBackend.CreateCommand command,
        CancellationToken cancellationToken)
    {
        // This filter sets initial chat read positions
        var context = CommandContext.GetCurrent();
        if (Computed.IsInvalidating()) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);

        var idRange = await GetIdRange(command.ChatId, ChatEntryType.Text, false, cancellationToken).ConfigureAwait(false);
        await Commander.Call(
            new IChatReadPositionsBackend.SetCommand(command.UserId, command.ChatId, idRange.End - 1),
            true,
            cancellationToken
            ).ConfigureAwait(false);
    }

    // [CommandHandler]
    public virtual async Task<ChatEntry> UpsertEntry(
        IChatsBackend.UpsertEntryCommand command,
        CancellationToken cancellationToken)
    {
        var entry = command.Entry;
        var changeKind = entry.Id == 0 ? ChangeKind.Create : entry.IsRemoved ? ChangeKind.Remove : ChangeKind.Update;
        var chatId = entry.ChatId;

        var context = CommandContext.GetCurrent();
        if (Computed.IsInvalidating()) {
            var invChatEntry = context.Operation().Items.Get<ChatEntry>();
            if (invChatEntry != null)
                InvalidateTiles(chatId, entry.Type, invChatEntry.Id, changeKind);

            // Invalidate min-max Id range at last
            switch (changeKind) {
            case ChangeKind.Create:
                _ = GetIdRange(chatId, entry.Type, true, default);
                _ = GetIdRange(chatId, entry.Type, false, default);
                break;
            case ChangeKind.Remove:
                _ = GetIdRange(chatId, entry.Type, false, default);
                break;
            }
            return null!;
        }

        var parsedChatId = new ParsedChatId(chatId).AssertValid();
        var isPeer = parsedChatId.Kind.IsPeerAny();
        if (isPeer)
            _ = await GetOrCreatePeerChat(chatId, cancellationToken).ConfigureAwait(false);

        // Injecting mention names into the markup
        if (entry.Type == ChatEntryType.Text && entry.Content.Length > 0) {
            var content = entry.Content;
            var markup = MarkupParser.Parse(content);
            var mentionNamer = new MentionNamer(ChatMentionResolverFactory.Create(chatId));
            markup = await mentionNamer.Rewrite(markup, cancellationToken).ConfigureAwait(false);
            content = MarkupFormatter.Default.Format(markup);
            entry = entry with { Content = content };
        }

        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var __ = dbContext.ConfigureAwait(false);

        var dbEntry = await DbUpsertEntry(dbContext, entry, command.HasAttachments, cancellationToken).ConfigureAwait(false);
        entry = dbEntry.ToModel();
        context.Operation().Items.Set(entry);

        if (entry.Type is not ChatEntryType.Text || entry.IsStreaming)
            return entry;

        // Let's enqueue the TextEntryChangedEvent
        var authorId = entry.AuthorId;
        var chatAuthor = await ChatAuthorsBackend.Get(chatId, authorId, true, cancellationToken).ConfigureAwait(false);
        var userId = chatAuthor!.UserId;
        new TextEntryChangedEvent(chatId, entry.Id, authorId, entry.Content, changeKind)
            .EnqueueOnCompletion(Queues.Chats.ShardBy(chatId), Queues.Users.ShardBy(userId));
        return entry;
    }

    // [CommandHandler]
    public virtual async Task<TextEntryAttachment> CreateTextEntryAttachment(
        IChatsBackend.CreateTextEntryAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        var attachment = command.Attachment;
        var context = CommandContext.GetCurrent();
        if (Computed.IsInvalidating()) {
            InvalidateTiles(command.Attachment.ChatId, ChatEntryType.Text, command.Attachment.EntryId, ChangeKind.Update);
            return default!;
        }

        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var __ = dbContext.ConfigureAwait(false);

        var compositeId = DbChatEntry.ComposeId(attachment.ChatId, ChatEntryType.Text, attachment.EntryId);
        var dbChatEntry = await dbContext.ChatEntries
                .FindAsync(DbKey.Compose(compositeId), cancellationToken)
                .ConfigureAwait(false)
            ?? throw StandardError.NotFound<ChatEntry>();
        if (dbChatEntry.IsRemoved)
            throw StandardError.Constraint("Removed chat entries cannot be modified.");

        attachment = attachment with {
            Version = VersionGenerator.NextVersion(),
        };
        var dbAttachment = new DbTextEntryAttachment(attachment);
        dbContext.Add(dbAttachment);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        attachment = dbAttachment.ToModel();
        context.Operation().Items.Set(attachment);
        return attachment;
    }

    // Event handlers

    [EventHandler]
    public virtual async Task OnNewUserEvent(NewUserEvent @event, CancellationToken cancellationToken)
    {
        await JoinAnnouncementsChat(@event.UserId, cancellationToken).ConfigureAwait(false);

        if (HostInfo.IsDevelopmentInstance)
            await JoinDefaultChatIfAdmin(@event.UserId, cancellationToken).ConfigureAwait(false);
    }

    // Protected methods

    [ComputeMethod]
    protected virtual async Task<long> GetMinId(
        string chatId,
        ChatEntryType entryType,
        CancellationToken cancellationToken)
    {
        var dbContext = CreateDbContext();
        await using var _ = dbContext.ConfigureAwait(false);

        return await dbContext.ChatEntries.AsQueryable()
            .Where(e => e.ChatId == chatId && e.Type == entryType)
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected void InvalidateTiles(string chatId, ChatEntryType entryType, long entryId, ChangeKind changeKind)
    {
        // Invalidate global entry counts
        switch (changeKind) {
        case ChangeKind.Create:
            _ = GetEntryCount(chatId, entryType, null, false, default);
            _ = GetEntryCount(chatId, entryType, null, true, default);
            break;
        case ChangeKind.Remove:
            _ = GetEntryCount(chatId, entryType, null, false, default);
            break;
        }

        // Invalidate GetTile & GetEntryCount for chat tiles
        foreach (var idTile in IdTileStack.GetAllTiles(entryId)) {
            if (idTile.Layer.Smaller == null) {
                // Larger tiles are composed out of smaller tiles,
                // so we have to invalidate just the smallest one.
                // And the tile with includeRemoved == false is based on
                // a tile with includeRemoved == true, so we have to invalidate
                // just this tile.
                _ = GetTile(chatId, entryType, idTile.Range, true, default);
            }
            switch (changeKind) {
            case ChangeKind.Create:
                _ = GetEntryCount(chatId, entryType, idTile.Range, true, default);
                _ = GetEntryCount(chatId, entryType, idTile.Range, false, default);
                break;
            case ChangeKind.Remove:
                _ = GetEntryCount(chatId, entryType, idTile.Range, false, default);
                break;
            }
        }
    }

    protected async Task<DbChatEntry> DbUpsertEntry(
        ChatDbContext dbContext,
        ChatEntry entry,
        bool hasAttachments,
        CancellationToken cancellationToken)
    {
        // AK: Suspicious - probably can lead to performance issues
        // AY: Yes, but the goal is to have a dense sequence here;
        //     later we'll change this to something that's more performant.
        var isNew = entry.Id == 0;
        var entryType = entry.Type;
        DbChatEntry dbEntry;
        if (isNew) {
            var id = await DbNextEntryId(dbContext, entry.ChatId, entryType, cancellationToken).ConfigureAwait(false);
            entry = entry with {
                Id = id,
                Version = VersionGenerator.NextVersion(),
                BeginsAt = Clocks.SystemClock.Now,
            };
            dbEntry = new (entry) {
                HasAttachments = hasAttachments,
            };

            dbContext.Add(dbEntry);
        }
        else {
            var compositeId = DbChatEntry.ComposeId(entry.ChatId, entryType, entry.Id);
            dbEntry = await dbContext.ChatEntries
                .FindAsync(DbKey.Compose(compositeId), cancellationToken)
                .ConfigureAwait(false)
                ?? throw StandardError.NotFound<ChatEntry>();
            VersionChecker.RequireExpected(dbEntry.Version, entry.Version);
            if (dbEntry.IsRemoved)
                throw StandardError.Constraint("Removed chat entries cannot be modified.");
            entry = entry with {
                Version = VersionGenerator.NextVersion(dbEntry.Version),
            };
            dbEntry.UpdateFrom(entry);
            dbContext.Update(dbEntry);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return dbEntry;
    }

    // Private / internal methods

    private async Task JoinAnnouncementsChat(string userId, CancellationToken cancellationToken)
    {
        var chatId = Constants.Chat.AnnouncementsChatId;
        var chatAuthor = await ChatAuthorsBackend.GetOrCreate(
                chatId,
                userId,
                false,
                cancellationToken)
            .ConfigureAwait(false);

        if (!HostInfo.IsDevelopmentInstance)
            return;

        var account = await AccountsBackend.Get(userId, cancellationToken).ConfigureAwait(false);
        if (account == null || !account.IsAdmin)
            return;

        await AddOwner(chatId, chatAuthor, cancellationToken).ConfigureAwait(false);
    }

    private async Task JoinDefaultChatIfAdmin(string userId, CancellationToken cancellationToken)
    {
        var account = await AccountsBackend.Get(userId, cancellationToken).ConfigureAwait(false);
        if (account == null || !account.IsAdmin)
            return;

        var chatId = Constants.Chat.DefaultChatId;
        var chatAuthor = await ChatAuthorsBackend.GetOrCreate(
                chatId,
                userId,
                false,
                cancellationToken)
            .ConfigureAwait(false);

        await AddOwner(chatId, chatAuthor, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddOwner(string chatId, ChatAuthorFull chatAuthor, CancellationToken cancellationToken)
    {
        var ownerRole = await ChatRolesBackend.GetSystem(chatId, SystemChatRole.Owner, cancellationToken)
            .ConfigureAwait(false);
        if (ownerRole == null)
            return;

        var createOwnersRoleCmd = new IChatRolesBackend.ChangeCommand(chatId,
            ownerRole.Id,
            null,
            new Change<ChatRoleDiff> {
                Update = new ChatRoleDiff {
                    AuthorIds = new SetDiff<ImmutableArray<Symbol>, Symbol> {
                        AddedItems = ImmutableArray<Symbol>.Empty.Add(chatAuthor.Id),
                    },
                },
            });
        await Commander.Call(createOwnersRoleCmd, cancellationToken).ConfigureAwait(false);
    }

    internal Task<long> DbNextEntryId(
        ChatDbContext dbContext,
        string chatId,
        ChatEntryType entryType,
        CancellationToken cancellationToken)
        => DbChatEntryIdGenerator.Next(dbContext, new DbChatEntryShardRef(chatId, entryType), cancellationToken);

    private async Task<ChatAuthorRules> GetPeerChatRules(
        string chatId, string chatPrincipalId,
        CancellationToken cancellationToken)
    {
        var parsedChatId = new ParsedChatId(chatId);
        if (parsedChatId.Kind != ChatIdKind.PeerFull)
            return ChatAuthorRules.None(chatId);

        var (userId1, userId2) = (parsedChatId.UserId1.Id, parsedChatId.UserId2.Id);
        var parsedChatPrincipalId = new ChatPrincipalId(chatPrincipalId);
        if (!parsedChatPrincipalId.IsValid)
            return ChatAuthorRules.None(chatId);

        var userId = parsedChatPrincipalId.UserId.Id;
        var chatAuthor = (ChatAuthorFull)null!;
        if (userId.IsEmpty) {
            var chatAuthorId = parsedChatPrincipalId.AuthorId.Id;
            if (!chatAuthorId.IsEmpty)
                chatAuthor = await ChatAuthorsBackend
                    .Get(chatId, chatAuthorId, false, cancellationToken)
                    .ConfigureAwait(false);
            userId = chatAuthor?.UserId ?? default;
        }

        var otherUserId = (userId1, userId2).OtherThan(userId);
        if (userId.IsEmpty || otherUserId.IsEmpty) // One of these users should be chatPrincipalId
            return ChatAuthorRules.None(chatId);

        var account = await AccountsBackend.Get(userId, cancellationToken).ConfigureAwait(false);
        if (account == null)
            return ChatAuthorRules.None(chatId);

        var otherAccount = await AccountsBackend.Get(otherUserId, cancellationToken).ConfigureAwait(false);
        if (otherAccount == null)
            return ChatAuthorRules.None(chatId);

        return new(chatId, chatAuthor, account, ChatPermissions.Write.AddImplied());
    }

    private async Task<Chat> GetOrCreatePeerChat(Symbol chatId, CancellationToken cancellationToken)
    {
        var chat = await Get(chatId, cancellationToken).ConfigureAwait(false);
        if (chat != null)
            return chat;
        return await CreatePeerChat(chatId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Chat> CreatePeerChat(Symbol chatId, CancellationToken cancellationToken)
    {
        _ = new ParsedChatId(chatId).AssertPeerFull();
        var cmd = new IChatsBackend.ChangeChatCommand(chatId, null, new() {
            Create = new ChatDiff() { ChatType = ChatType.Peer },
        });
        var chat = await Commander.Call(cmd, true, cancellationToken).ConfigureAwait(false);
        return chat!;
    }
}
