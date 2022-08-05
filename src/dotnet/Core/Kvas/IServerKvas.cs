namespace ActualChat.Kvas;

public interface IServerKvas : IComputeService
{
    [ComputeMethod]
    Task<string?> Get(Session session, string key, CancellationToken cancellationToken = default);

    [CommandHandler]
    Task Set(SetCommand command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task SetMany(SetManyCommand command, CancellationToken cancellationToken = default);

    [DataContract]
    public record SetCommand(
        [property: DataMember(Order = 0)] Session Session,
        [property: DataMember(Order = 1)] string Key,
        [property: DataMember(Order = 2)] string? Value) : ICommand<Unit>;

    [DataContract]
    public record SetManyCommand(
        [property: DataMember(Order = 0)] Session Session,
        [property: DataMember(Order = 1)] (string Key, string? Value)[] Items) : ICommand<Unit>;
}
