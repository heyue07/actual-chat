namespace ActualChat;

public interface IChange : IRequirementTarget
{
    ChangeKind Kind { get; }
    bool IsValid();
}

[DataContract]
public record Change<TCreate, TUpdate> : IChange
{
    [DataMember] public Option<TCreate> Create { get; init; }
    [DataMember] public Option<TUpdate> Update { get; init; }
    [DataMember] public bool Remove { get; init; }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public ChangeKind Kind {
        get {
            this.RequireValid();
            if (Create.HasValue)
                return ChangeKind.Create;
            if (Update.HasValue)
                return ChangeKind.Update;
            return ChangeKind.Remove;
        }
    }

    public bool IsValid()
    {
        var hasCreate = Create.HasValue;
        var hasUpdate = Update.HasValue;
        var hasRemove = Remove;
        return hasCreate != hasUpdate != hasRemove && !(hasCreate && hasUpdate && hasRemove);
    }

    public bool IsCreate(out TCreate create)
    {
        if (!Create.IsSome(out create!))
            return false;
        return true;
    }

    public bool IsUpdate(out TUpdate update)
    {
        if (!Update.IsSome(out update!))
            return false;
        return true;
    }

    public bool IsRemove()
    {
        if (!Remove)
            return false;
        return true;
    }
}

public record Change<T> : Change<T, T>;
