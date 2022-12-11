using System.Security.Claims;
using ActualChat.Chat.Db;
using ActualChat.Contacts;
using ActualChat.Hosting;
using ActualChat.Users;
using Microsoft.EntityFrameworkCore;
using Stl.Fusion.EntityFramework;

namespace ActualChat.Chat;

public class ChatsUpgradeBackend : DbServiceBase<ChatDbContext>, IChatsUpgradeBackend
{
    private IAccountsBackend AccountsBackend { get; }
    private IAuthorsBackend AuthorsBackend { get; }
    private IAuthorsUpgradeBackend AuthorsUpgradeBackend { get; }
    private IRolesBackend RolesBackend { get; }
    private IContactsBackend ContactsBackend { get; }
    private IChatsBackend Backend { get; }

    public ChatsUpgradeBackend(IServiceProvider services) : base(services)
    {
        AccountsBackend = services.GetRequiredService<IAccountsBackend>();
        AuthorsBackend = services.GetRequiredService<IAuthorsBackend>();
        AuthorsUpgradeBackend = services.GetRequiredService<IAuthorsUpgradeBackend>();
        RolesBackend = services.GetRequiredService<IRolesBackend>();
        ContactsBackend = services.GetRequiredService<IContactsBackend>();
        Backend = services.GetRequiredService<IChatsBackend>();
    }

    // [CommandHandler]
    public virtual async Task UpgradeChat(
        IChatsUpgradeBackend.UpgradeChatCommand command,
        CancellationToken cancellationToken)
    {
        var chatId = command.ChatId.Require();
        var context = CommandContext.GetCurrent();

        if (Computed.IsInvalidating()) {
            var invChat = context.Operation().Items.Get<Chat>()!;
            _ = Backend.Get(invChat.Id, default);
            return;
        }

        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var __ = dbContext.ConfigureAwait(false);

        var dbChat = await dbContext.Chats
            .Include(c => c.Owners)
            .SingleOrDefaultAsync(c => c.Id == chatId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (dbChat == null)
            return;

        Log.LogInformation("Upgrading chat #{ChatId}: '{ChatTitle}' ({ChatType})",
            chatId, dbChat.Title, dbChat.Kind);

        var chat = dbChat.ToModel();
        if (chat.Id.IsPeerChatId(out var peerChatId)) {
            // Peer chat
            await peerChatId.UserIds
                .ToArray()
                .Select(userId => AuthorsBackend.GetOrCreate(chatId, userId, cancellationToken))
                .Collect(0)
                .ConfigureAwait(false);
            var (userId1, userId2) = peerChatId.UserIds;
            var contactTask1 = ContactsBackend.GetOrCreateUserContact(userId1, userId2, cancellationToken);
            var contactTask2 = ContactsBackend.GetOrCreateUserContact(userId2, userId1, cancellationToken);
            await Task.WhenAll(contactTask1, contactTask2).ConfigureAwait(false);
        }
        else {
            // Group chat

            // Removing duplicate system roles
            var systemDbRoles = await dbContext.Roles
                .Where(r => r.ChatId == chatId.Value && r.SystemRole != SystemRole.None)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var group in systemDbRoles.GroupBy(r => r.SystemRole)) {
                if (group.Count() <= 1)
                    continue;
                foreach (var dbChatRole in group.Skip(1))
                    dbContext.Roles.Remove(dbChatRole);
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Reload system roles
            systemDbRoles = await dbContext.Roles
                .Where(r => r.ChatId == chatId.Value && r.SystemRole != SystemRole.None)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var ownerIds = dbChat.Owners.Select(o => new UserId(o.DbUserId)).ToArray();
            var ownerAuthors = await ownerIds
                .Select(userId => AuthorsBackend.GetOrCreate(chatId, userId, cancellationToken))
                .Collect()
                .ConfigureAwait(false);

            if (ownerIds.Length > 0) {
                var dbOwnerRole = systemDbRoles.SingleOrDefault(r => r.SystemRole == SystemRole.Owner);
                if (dbOwnerRole == null) {
                    var createOwnersRoleCmd = new IRolesBackend.ChangeCommand(chatId, default, null, new() {
                        Create = new RoleDiff() {
                            SystemRole = SystemRole.Owner,
                            Permissions = ChatPermissions.Owner,
                            AuthorIds = new SetDiff<ImmutableArray<AuthorId>, AuthorId>() {
                                AddedItems = ImmutableArray<AuthorId>.Empty.AddRange(ownerAuthors.Select(a => a.Id)),
                            },
                        },
                    });
                    await Commander.Call(createOwnersRoleCmd, cancellationToken).ConfigureAwait(false);
                }
                else {
                    // We want another transaction view here
                    using var dbContext2 = CreateDbContext();
                    var ownerRoleAuthorIds = (await dbContext2.Authors
                        .Where(a => a.ChatId == chatId.Value && a.UserId != null && a.Roles.Any(r => r.DbRoleId == dbOwnerRole.Id))
                        .Select(a => a.Id)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false))
                        .Select(x => (Symbol)x)
                        .ToHashSet();
                    var missingAuthors = ownerAuthors.Where(a => !ownerRoleAuthorIds.Contains(a.Id));

                    var ownerRoleId = new RoleId(dbOwnerRole.Id);
                    var changeOwnersRoleCmd = new IRolesBackend.ChangeCommand(
                        chatId, ownerRoleId, dbOwnerRole.Version,
                        new() {
                            Update = new RoleDiff() {
                                Permissions = ChatPermissions.Owner,
                                AuthorIds = new SetDiff<ImmutableArray<AuthorId>, AuthorId>() {
                                    AddedItems = ImmutableArray<AuthorId>.Empty.AddRange(missingAuthors.Select(a => a.Id)),
                                },
                            },
                        });
                    await Commander.Call(changeOwnersRoleCmd, cancellationToken).ConfigureAwait(false);
                }
            }

            var dbAnyoneRole = systemDbRoles.SingleOrDefault(r => r.SystemRole == SystemRole.Anyone);
            if (dbAnyoneRole == null) {
                var createAnyoneRoleCmd = new IRolesBackend.ChangeCommand(chatId, default, null, new() {
                    Create = new RoleDiff() {
                        SystemRole = SystemRole.Anyone,
                        Permissions =
                            ChatPermissions.Write
                            | ChatPermissions.Invite
                            | ChatPermissions.SeeMembers
                            | ChatPermissions.Leave,
                    },
                });
                await Commander.Call(createAnyoneRoleCmd, cancellationToken).ConfigureAwait(false);
            }
        }

        // NOTE(AY): Uncomment this once we're completely sure there are no issues w/ owners -> roles upgrade
        // dbChat.Owners.Clear();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        chat = dbChat.ToModel();
        context.Operation().Items.Set(chat);
    }

    // [CommandHandler]
    public virtual async Task<Chat> CreateAnnouncementsChat(
        IChatsUpgradeBackend.CreateAnnouncementsChatCommand command,
        CancellationToken cancellationToken)
    {
        var chatId = Constants.Chat.AnnouncementsChatId;
        if (Computed.IsInvalidating()) {
            _ = Backend.Get(chatId, default);
            return default!;
        }

        var usersTempBackend = Services.GetRequiredService<IUsersUpgradeBackend>();
        var hostInfo = Services.GetRequiredService<HostInfo>();
        var userIds = await usersTempBackend.ListAllUserIds(cancellationToken).ConfigureAwait(false);

        var admin = await AccountsBackend.Get(Constants.User.Admin.UserId, cancellationToken)
            .Require()
            .ConfigureAwait(false);
        var creatorId = admin.Id;

        var userIdByEmail = new Dictionary<string, UserId>(StringComparer.OrdinalIgnoreCase);
        foreach (var userId in userIds) {
            var account = await AccountsBackend.Get(userId, cancellationToken).ConfigureAwait(false);
            if (account == null)
                continue;

            var user = account.User;
            if (user.Claims.Count == 0)
                continue;
            if (!user.Claims.TryGetValue(ClaimTypes.Email, out var email))
                continue;

            if (hostInfo.IsDevelopmentInstance) {
                if (email.OrdinalIgnoreCaseEndsWith("actual.chat"))
                    userIdByEmail.Add(email, userId);
            }
            else {
                if (OrdinalIgnoreCaseEquals(email, "alex.yakunin@actual.chat") || OrdinalIgnoreCaseEquals(email, "alexey.kochetov@actual.chat"))
                    userIdByEmail.Add(email, userId);
            }
        }

        if (creatorId.IsNone) {
            if (userIdByEmail.TryGetValue("alex.yakunin@actual.chat", out var temp))
                creatorId = temp;
            else if (userIdByEmail.Count > 0)
                creatorId = userIdByEmail.First().Value;
        }
        if (creatorId.IsNone)
            throw StandardError.Constraint("Creator user not found");

        var changeCommand = new IChatsBackend.ChangeCommand(chatId, null, new() {
            Create = new ChatDiff {
                Title = "Actual.chat Announcements",
                IsPublic = true,
            },
        }, creatorId);
        var chat = (await Commander.Call(changeCommand, true, cancellationToken).ConfigureAwait(false))!;

        var anyoneRole = await RolesBackend
            .GetSystem(chatId, SystemRole.Anyone, cancellationToken)
            .Require()
            .ConfigureAwait(false);

        var changeAnyoneRoleCmd = new IRolesBackend.ChangeCommand(chatId, anyoneRole.Id, null, new() {
            Update = new RoleDiff() {
                Permissions = ChatPermissions.Invite,
            },
        });
        await Commander.Call(changeAnyoneRoleCmd, cancellationToken).ConfigureAwait(false);

        var authorByUserId = new Dictionary<UserId, AuthorFull>();
        foreach (var userId in userIds) {
            // join existent users to the chat
           var author = await AuthorsBackend.GetOrCreate(chatId, userId, cancellationToken).ConfigureAwait(false);
           authorByUserId.Add(userId, author);
        }

        var ownerRole = await RolesBackend
            .GetSystem(chatId, SystemRole.Owner, cancellationToken)
            .Require()
            .ConfigureAwait(false);
        var ownerAuthorIds = ImmutableArray<AuthorId>.Empty;
        foreach (var userId in userIdByEmail.Values) {
            if (userId == creatorId)
                continue;
            if (!authorByUserId.TryGetValue(userId, out var author))
                continue;
            ownerAuthorIds = ownerAuthorIds.Add(author.Id);
        }

        if (ownerAuthorIds.Length > 0) {
            var changeOwnerRoleCmd = new IRolesBackend.ChangeCommand(chatId, ownerRole.Id, null, new() {
                Update = new RoleDiff {
                    AuthorIds = new SetDiff<ImmutableArray<AuthorId>, AuthorId> {
                        AddedItems = ownerAuthorIds,
                    }
                },
            });
            await Commander.Call(changeOwnerRoleCmd, cancellationToken).ConfigureAwait(false);
        }

        return chat;
    }

    // [CommandHandler]
    public virtual async Task FixCorruptedReadPositions(
        IChatsUpgradeBackend.FixCorruptedReadPositionsCommand command,
        CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return; // It just spawns other commands, so nothing to do here

        var readPositionsBackend = Services.GetRequiredService<IReadPositionsBackend>();
        var usersTempBackend = Services.GetRequiredService<IUsersUpgradeBackend>();

        var userIds = await usersTempBackend.ListAllUserIds(cancellationToken).ConfigureAwait(false);
        foreach (var userId in userIds) {
            var chatIds = await AuthorsUpgradeBackend.ListChatIds(userId, cancellationToken).ConfigureAwait(false);
            foreach (var chatId in chatIds) {
                var lastReadEntryId = await readPositionsBackend.Get(userId, chatId, cancellationToken).ConfigureAwait(false);
                if (lastReadEntryId.GetValueOrDefault() == 0)
                    continue;

                var idRange = await Backend.GetIdRange(chatId, ChatEntryKind.Text, false, cancellationToken).ConfigureAwait(false);
                var lastEntryId = idRange.End - 1;
                if (lastEntryId >= lastReadEntryId)
                    continue;

                // since it was corrupted for some time and user might not know that there are some new message
                // let's show at least 1 unread message, so user could pay attention to this chat
                Log.LogInformation(
                    "Fixing corrupted last read position for user #{UserId} at chat #{ChatId}: {CurrentLastReadEntryId} -> {FixedLastReadEntryId}",
                    userId,
                    chatId,
                    lastReadEntryId,
                    lastEntryId - 1);
                var setCmd = new IReadPositionsBackend.SetCommand(userId, chatId,  lastEntryId - 1, true);
                await Commander.Call(setCmd, true, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
