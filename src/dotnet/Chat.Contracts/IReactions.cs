namespace ActualChat.Chat;

public interface IReactions : IComputeService
{
    [ComputeMethod]
    Task<Reaction?> Get(Session session, TextEntryId entryId, CancellationToken cancellationToken);

    [ComputeMethod]
    Task<ImmutableArray<ReactionSummary>> ListSummaries(
        Session session,
        TextEntryId entryId,
        CancellationToken cancellationToken);

    [CommandHandler]
    Task React(ReactCommand command, CancellationToken cancellationToken);

    [DataContract]
    public sealed record ReactCommand(
        [property: DataMember] Session Session,
        [property: DataMember] Reaction Reaction
    ) : ISessionCommand<Unit>;
}
