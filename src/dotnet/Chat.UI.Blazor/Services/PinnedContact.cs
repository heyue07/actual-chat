namespace ActualChat.Chat.UI.Blazor.Services;

[StructLayout(LayoutKind.Auto)]
public readonly record struct PinnedContact(
    ContactId Id,
    Moment Recency = default)
{
    public static implicit operator PinnedContact(ContactId id) => new(id);

    // Equality must rely on Id only
    public bool Equals(PinnedContact other)
        => Id.Equals(other.Id);
    public override int GetHashCode()
        => Id.GetHashCode();
}
