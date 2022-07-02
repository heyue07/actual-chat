namespace ActualChat.Chat;

public interface IChats : IComputeService
{
    [ComputeMethod(KeepAliveTime = 1)]
    Task<Chat?> Get(Session session, string chatId, CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<Chat[]> GetChats(Session session, CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<long> GetEntryCount(
        Session session,
        string chatId,
        ChatEntryType entryType,
        Range<long>? idTileRange,
        CancellationToken cancellationToken);

    // Note that it returns (firstId, lastId + 1) range!
    [ComputeMethod(KeepAliveTime = 1)]
    Task<Range<long>> GetIdRange(
        Session session,
        string chatId,
        ChatEntryType entryType,
        CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<Range<long>> GetLastIdTile0(
        Session session,
        string chatId,
        ChatEntryType entryType,
        CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<Range<long>> GetLastIdTile1(
        Session session,
        string chatId,
        ChatEntryType entryType,
        CancellationToken cancellationToken);

    // Client-side method always skips entries with IsRemoved flag
    [ComputeMethod(KeepAliveTime = 1)]
    Task<ChatTile> GetTile(
        Session session,
        string chatId,
        ChatEntryType entryType,
        Range<long> idTileRange,
        CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<ChatAuthorRules> GetRules(
        Session session,
        string chatId,
        CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<bool> CanJoin(
        Session session,
        string chatId,
        CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<ImmutableArray<TextEntryAttachment>> GetTextEntryAttachments(
        Session session, string chatId, long entryId,
        CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<bool> CanSendPeerChatMessage(Session session, string chatPrincipalId, CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<string?> GetPeerChatId(Session session, string chatPrincipalId, CancellationToken cancellationToken);

    Task<MentionCandidate[]> GetMentionCandidates(Session session, string chatId, CancellationToken cancellationToken);

    // Commands

    [CommandHandler]
    Task<Chat> CreateChat(CreateChatCommand command, CancellationToken cancellationToken);
    [CommandHandler]
    Task<Unit> UpdateChat(UpdateChatCommand command, CancellationToken cancellationToken);
    [CommandHandler]
    Task<Unit> JoinChat(JoinChatCommand command, CancellationToken cancellationToken);
    [CommandHandler]
    Task<ChatEntry> CreateTextEntry(CreateTextEntryCommand command, CancellationToken cancellationToken);
    [CommandHandler]
    Task RemoveTextEntry(RemoveTextEntryCommand command, CancellationToken cancellationToken);

    [DataContract]
    public sealed record CreateChatCommand(
        [property: DataMember] Session Session,
        [property: DataMember] string Title
        ) : ISessionCommand<Chat>
    {
        [DataMember] public bool IsPublic { get; init; }
    }

    [DataContract]
    public sealed record UpdateChatCommand(
        [property: DataMember] Session Session,
        [property: DataMember] Chat Chat
        ) : ISessionCommand<Unit>;

    [DataContract]
    public sealed record JoinChatCommand(
        [property: DataMember] Session Session,
        [property: DataMember] string ChatId
        ) : ISessionCommand<Unit>;

    [DataContract]
    public sealed record CreateTextEntryCommand(
        [property: DataMember] Session Session,
        [property: DataMember] string ChatId,
        [property: DataMember] string Text) : ISessionCommand<ChatEntry>
    {
        [DataMember] public ImmutableArray<TextEntryAttachmentUpload> Attachments { get; set; } =
            ImmutableArray<TextEntryAttachmentUpload>.Empty;
        [DataMember] public long? RepliedChatEntryId { get; set; }
    }

    [DataContract]
    public sealed record RemoveTextEntryCommand(
        [property: DataMember] Session Session,
        [property: DataMember] string ChatId,
        [property: DataMember] long EntryId
        ) : ISessionCommand<Unit>;
}
