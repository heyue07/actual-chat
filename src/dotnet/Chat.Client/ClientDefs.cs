using ActualChat.Users;
using RestEase;

namespace ActualChat.Chat.Client;

[BasePath("chats")]
public interface IChatsClientDef
{
    [Get(nameof(Get))]
    Task<Chat?> Get(Session session, string chatId, CancellationToken cancellationToken);

    [Get(nameof(List))]
    Task<ImmutableArray<Chat>> List(Session session, CancellationToken cancellationToken);

    [Get(nameof(GetRules))]
    Task<AuthorRules> GetRules(
        Session session,
        string chatId,
        CancellationToken cancellationToken);

    [Get(nameof(GetSummary))]
    Task<ChatSummary?> GetSummary(
        Session session,
        string chatId,
        CancellationToken cancellationToken);

    [Get(nameof(GetIdRange))]
    Task<Range<long>> GetIdRange(
        Session session,
        string chatId,
        ChatEntryType entryType,
        CancellationToken cancellationToken);

    [Get(nameof(GetEntryCount))]
    Task<long> GetEntryCount(
        Session session,
        string chatId,
        ChatEntryType entryType,
        Range<long>? idTileRange,
        CancellationToken cancellationToken);

    [Get(nameof(GetTile))]
    Task<ChatTile> GetTile(
        Session session,
        string chatId,
        ChatEntryType entryType,
        Range<long> idTileRange,
        CancellationToken cancellationToken);

    [Get(nameof(CanJoin))]
    Task<bool> CanJoin(
        Session session,
        string chatId,
        CancellationToken cancellationToken);

    [Get(nameof(CanPeerChat))]
    Task<bool> CanPeerChat(Session session, string chatId, string authorId, CancellationToken cancellationToken);
    [Get(nameof(GetPeerChatId))]
    Task<string?> GetPeerChatId(Session session, string chatId, string authorId, CancellationToken cancellationToken);
    [Get(nameof(GetPeerChatContact))]
    Task<Contact?> GetPeerChatContact(Session session, string chatId, CancellationToken cancellationToken);

    [Get(nameof(ListMentionableAuthors))]
    Task<ImmutableArray<Author>> ListMentionableAuthors(Session session, string chatId, CancellationToken cancellationToken);
    [Get(nameof(FindNext))]
    Task<ChatEntry?> FindNext(Session session, string chatId, long? startEntryId, string text, CancellationToken cancellationToken);

    [Post(nameof(Change))]
    Task<Chat> Change([Body] IChats.ChangeCommand command, CancellationToken cancellationToken);
    [Post(nameof(Join))]
    Task<Unit> Join([Body] IChats.JoinCommand command, CancellationToken cancellationToken);
    [Post(nameof(Leave))]
    Task Leave([Body] IChats.LeaveCommand command, CancellationToken cancellationToken);

    [Post(nameof(UpsertTextEntry))]
    Task<ChatEntry> UpsertTextEntry([Body] IChats.UpsertTextEntryCommand command, CancellationToken cancellationToken);
    [Post(nameof(RemoveTextEntry))]
    Task RemoveTextEntry([Body] IChats.RemoveTextEntryCommand command, CancellationToken cancellationToken);
}

[BasePath("authors")]
public interface IAuthorsClientDef
{
    [Get(nameof(Get))]
    Task<Author?> Get(Session session, string chatId, string authorId, CancellationToken cancellationToken);
    [Get(nameof(GetOwn))]
    Task<AuthorFull?> GetOwn(Session session, string chatId, CancellationToken cancellationToken);
    [Get(nameof(GetFull))]
    Task<AuthorFull?> GetFull(Session session, string chatId, string authorId, CancellationToken cancellationToken);
    [Get(nameof(ListOwnChatIds))]
    Task<ImmutableArray<Symbol>> ListOwnChatIds(Session session, CancellationToken cancellationToken);
    [Get(nameof(ListAuthorIds))]
    Task<ImmutableArray<Symbol>> ListAuthorIds(Session session, string chatId, CancellationToken cancellationToken);
    [Get(nameof(ListUserIds))]
    Task<ImmutableArray<Symbol>> ListUserIds(Session session, string chatId, CancellationToken cancellationToken);

    [Get(nameof(GetAuthor))]
    Task<Author?> GetAuthor(Session session, string chatId, string authorId, CancellationToken cancellationToken);
    [Get(nameof(GetAuthorPresence))]
    Task<Presence> GetAuthorPresence(Session session, string chatId, string authorId, CancellationToken cancellationToken);
    [Get(nameof(CanAddToContacts))]
    Task<bool> CanAddToContacts(Session session, string chatId, string authorId, CancellationToken cancellationToken);

    [Post(nameof(AddToContacts))]
    Task AddToContacts([Body] IAuthors.AddToContactsCommand command, CancellationToken cancellationToken);
    [Post(nameof(CreateAuthors))]
    Task CreateAuthors([Body] IAuthors.CreateAuthorsCommand command, CancellationToken cancellationToken);
    [Post(nameof(SetAvatar))]
    Task SetAvatar([Body] IAuthors.SetAvatarCommand command, CancellationToken cancellationToken);
}

[BasePath("roles")]
public interface IRolesClientDef
{
    [Get(nameof(Get))]
    Task<Role?> Get(Session session, string chatId, string roleId, CancellationToken cancellationToken);

    [Get(nameof(List))]
    Task<ImmutableArray<Role>> List(Session session, string chatId, CancellationToken cancellationToken);
    [Get(nameof(ListAuthorIds))]
    Task<ImmutableArray<Symbol>> ListAuthorIds(Session session, string chatId, string roleId, CancellationToken cancellationToken);

    [Post(nameof(Change))]
    Task<Role> Change([Body] IRoles.ChangeCommand command, CancellationToken cancellationToken);
}

[BasePath("mentions")]
public interface IMentionsClientDef
{
    [Get(nameof(GetLastOwn))]
    Task<Mention?> GetLastOwn(
        Session session,
        Symbol chatId,
        CancellationToken cancellationToken);
}

[BasePath("reactions")]
public interface IReactionsClientDef
{
    [Get(nameof(List))]
    Task<ImmutableArray<ReactionSummary>> List(
        Session session,
        Symbol chatEntryId,
        CancellationToken cancellationToken);

    [Post(nameof(React))]
    Task React([Body] IReactions.ReactCommand command, CancellationToken cancellationToken);
}
