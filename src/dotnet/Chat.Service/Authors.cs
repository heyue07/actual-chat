using ActualChat.Chat.Db;
using ActualChat.Contacts;
using ActualChat.Kvas;
using ActualChat.Users;
using Stl.Fusion.EntityFramework;

namespace ActualChat.Chat;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Authors : DbServiceBase<ChatDbContext>, IAuthors
{
    private IAuthorsBackend? _backend;
    private IChats? _chats;
    private IChatsBackend? _chatsBackend;
    private IContactsBackend? _contactsBackend;

    private IAccounts Accounts { get; }
    private IAccountsBackend AccountsBackend { get; }
    private IChats Chats => _chats ??= Services.GetRequiredService<IChats>();
    private IChatsBackend ChatsBackend => _chatsBackend ??= Services.GetRequiredService<IChatsBackend>();
    private IContactsBackend ContactsBackend => _contactsBackend ??= Services.GetRequiredService<IContactsBackend>();
    private IUserPresences UserPresences { get; }
    private IServerKvas ServerKvas { get; }
    private IAuthorsBackend Backend => _backend ??= Services.GetRequiredService<IAuthorsBackend>();

    public Authors(IServiceProvider services) : base(services)
    {
        Accounts = services.GetRequiredService<IAccounts>();
        AccountsBackend = services.GetRequiredService<IAccountsBackend>();
        UserPresences = services.GetRequiredService<IUserPresences>();
        ServerKvas = services.ServerKvas();
    }

    // [ComputeMethod]
    public virtual async Task<Author?> Get(
        Session session, string chatId, string authorId,
        CancellationToken cancellationToken)
    {
        var canRead = await CanRead(session, chatId, cancellationToken).ConfigureAwait(false);
        if (!canRead)
            return null;

        var author = await Backend.Get(chatId, authorId, cancellationToken).ConfigureAwait(false);
        return author;
    }

    // [ComputeMethod]
    public virtual async Task<AuthorFull?> GetOwn(
        Session session, string chatId,
        CancellationToken cancellationToken)
    {
        // This method is used by Chats.GetRules, etc., so it shouldn't check
        // the ability to access the chat, otherwise we'll hit the recursion here.

        var account = await Accounts.GetOwn(session, cancellationToken).ConfigureAwait(false);
        if (account != null)
            return await Backend.GetByUserId(chatId, account.Id, cancellationToken).ConfigureAwait(false);

        var kvas = ServerKvas.GetClient(session);
        var settings = await kvas.GetUnregisteredUserSettings(cancellationToken).ConfigureAwait(false);
        var authorId = settings.Chats.GetValueOrDefault(chatId);
        if (authorId.IsNullOrEmpty())
            return null;

        return await Backend.Get(chatId, authorId, cancellationToken).ConfigureAwait(false);
    }

    // [ComputeMethod]
    public virtual async Task<AuthorFull?> GetFull(
        Session session, string chatId, string authorId,
        CancellationToken cancellationToken)
    {
        var ownAuthor = await GetOwn(session, chatId, cancellationToken).Require().ConfigureAwait(false);
        if (ownAuthor.Id == authorId)
            return ownAuthor;

        var rules = await ChatsBackend.GetRules(chatId, ownAuthor.Id, cancellationToken).ConfigureAwait(false);
        if (!rules.Has(ChatPermissions.EditRoles))
            return null;

        return await Backend.Get(chatId, authorId, cancellationToken).ConfigureAwait(false);
    }

    // [ComputeMethod]
    public virtual async Task<Account?> GetAccount(
        Session session, string chatId, string authorId,
        CancellationToken cancellationToken)
    {
        // In fact, de-anonymizes the author
        var canRead = await CanRead(session, chatId, cancellationToken).ConfigureAwait(false);
        if (!canRead)
            return null;

        var author = await Backend.Get(chatId, authorId, cancellationToken).ConfigureAwait(false);
        if (author == null)
            return null;
        if (author.IsAnonymous || author.UserId.IsEmpty)
            return null;

        var account = await AccountsBackend.Get(author.UserId, cancellationToken).ConfigureAwait(false);
        return account;
    }

    // [ComputeMethod]
    public virtual async Task<ImmutableArray<Symbol>> ListAuthorIds(Session session, string chatId, CancellationToken cancellationToken)
    {
        var account = await Accounts.GetOwn(session, cancellationToken).ConfigureAwait(false);
        if (account == null)
            return ImmutableArray<Symbol>.Empty;

        var rules = await Chats.GetRules(session, chatId, cancellationToken).ConfigureAwait(false);
        if (!rules.CanSeeMembers())
            return ImmutableArray<Symbol>.Empty;

        return await Backend.ListAuthorIds(chatId, cancellationToken).ConfigureAwait(false);
    }

    // [ComputeMethod]
    public virtual async Task<ImmutableArray<Symbol>> ListUserIds(Session session, string chatId, CancellationToken cancellationToken)
    {
        var account = await Accounts.GetOwn(session, cancellationToken).ConfigureAwait(false);
        if (account == null)
            return ImmutableArray<Symbol>.Empty;

        return await Backend.ListUserIds(chatId, cancellationToken).ConfigureAwait(false);
    }

    // [ComputeMethod]
    public virtual async Task<Presence> GetAuthorPresence(
        Session session,
        string chatId,
        string authorId,
        CancellationToken cancellationToken)
    {
        var chat = await Chats.Get(session, chatId, cancellationToken).ConfigureAwait(false);
        if (chat == null)
            return Presence.Unknown;

        var author = await Backend.Get(chatId, authorId, cancellationToken).ConfigureAwait(false);
        if (author == null)
            return Presence.Offline;
        if (author.UserId.IsEmpty || author.IsAnonymous)
            return Presence.Unknown; // Important: we shouldn't report anonymous author presence
        return await UserPresences.Get(author.UserId.Value, cancellationToken).ConfigureAwait(false);
    }

    // [CommandHandler]
    public virtual async Task CreateAuthors(IAuthors.CreateAuthorsCommand command, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return; // It just spawns other commands, so nothing to do here

        var chatRules = await Chats.GetRules(command.Session, command.ChatId, cancellationToken).ConfigureAwait(false);
        chatRules.Require(ChatPermissions.Invite);

        foreach (var userId in command.UserIds)
            await Backend.GetOrCreate(command.ChatId, userId, cancellationToken).ConfigureAwait(false);
    }

    // [CommandHandler]
    public virtual async Task SetAvatar(IAuthors.SetAvatarCommand command, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return; // It just spawns other commands, so nothing to do here

        var (session, chatId, avatarId) = command;
        var canRead = await CanRead(session, chatId, cancellationToken).ConfigureAwait(false);
        if (!canRead)
            return;
        var author = await GetOwn(session, chatId, cancellationToken).ConfigureAwait(false);
        if (author == null)
            return;

        var setAvatarCommand = new IAuthorsBackend.SetAvatarCommand(chatId, author.Id, avatarId);
        await Commander.Call(setAvatarCommand, true, cancellationToken).ConfigureAwait(false);
    }

    // Private methods

    private async ValueTask<bool> CanRead(Session session, string chatId, CancellationToken cancellationToken)
    {
        var chat = await Chats.Get(session, chatId, cancellationToken).ConfigureAwait(false);
        if (chat == null)
            return false;

        var canRead = await Chats.HasPermissions(session, chatId, ChatPermissions.Read, cancellationToken).ConfigureAwait(false);
        return canRead;
    }
}
