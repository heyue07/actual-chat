using System.ComponentModel;
using ActualChat.Internal;
using Stl.Fusion.Blazor;

namespace ActualChat;

[DataContract]
[JsonConverter(typeof(SymbolIdentifierJsonConverter<PeerChatId>))]
[Newtonsoft.Json.JsonConverter(typeof(SymbolIdentifierNewtonsoftJsonConverter<PeerChatId>))]
[TypeConverter(typeof(SymbolIdentifierTypeConverter<PeerChatId>))]
[ParameterComparer(typeof(ByValueParameterComparer))]
[StructLayout(LayoutKind.Auto)]
public readonly struct PeerChatId : ISymbolIdentifier<PeerChatId>
{
    public static readonly string IdPrefix = "p-";
    public static PeerChatId None => default;

    [DataMember(Order = 0)]
    public Symbol Id { get; }

    // Parsed
    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public UserId UserId1 { get; }
    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public UserId UserId2 { get; }

    // Computed
    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public string Value => Id.Value;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public bool IsNone => Id.IsEmpty;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public (UserId UserId1, UserId UserId2) UserIds => (UserId1, UserId2);

    [JsonConstructor, Newtonsoft.Json.JsonConstructor]
    public PeerChatId(Symbol id) => this = Parse(id);
    public PeerChatId(string? id) => this = Parse(id);
    public PeerChatId(string? id, ParseOrNone _) => ParseOrNone(id);

    public PeerChatId(UserId userId1, UserId userId2, ParseOrNone _)
    {
        if (userId1.IsNone)
            return;
        if (userId2.IsNone)
            return;
        if (userId1 == userId2)
            return;

        (UserId1, UserId2) = (userId1, userId2).Sort();
        Id = Format(UserId1, UserId2);
    }

    public PeerChatId(UserId userId1, UserId userId2)
    {
        if (userId1.IsNone)
            throw new ArgumentOutOfRangeException(nameof(userId1));
        if (userId2.IsNone)
            throw new ArgumentOutOfRangeException(nameof(userId2));
        if (userId1 == userId2)
            throw new ArgumentOutOfRangeException(nameof(userId2), "Both user IDs are the same.");

        (UserId1, UserId2) = (userId1, userId2).Sort();
        Id = Format(UserId1, UserId2);
    }

    public PeerChatId(Symbol id, UserId userId1, UserId userId2, AssumeValid _)
    {
        if (id.IsEmpty) {
            this = None;
            return;
        }
        Id = id;
        UserId1 = userId1;
        UserId2 = userId2;
    }

    // Helpers

    public int IndexOf(UserId userId)
    {
        if (UserId1 == userId)
            return 0;
        if (UserId2 == userId)
            return 1;
        return -1;
    }

    // Conversion

    public override string ToString() => Value;
    public static implicit operator ChatId(PeerChatId source) => new(source.Id, source.UserId1, source.UserId2, AssumeValid.Option);
    public static implicit operator Symbol(PeerChatId source) => source.Id;
    public static implicit operator string(PeerChatId source) => source.Id.Value;

    // Equality

    public bool Equals(PeerChatId other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is PeerChatId other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
    public static bool operator ==(PeerChatId left, PeerChatId right) => left.Equals(right);
    public static bool operator !=(PeerChatId left, PeerChatId right) => !left.Equals(right);

    // Parsing

    private static string Format(UserId userId1, UserId userId2)
        => userId1.IsNone ? "" : $"{IdPrefix}{userId1}-{userId2}";

    public static PeerChatId Parse(string? s)
        => TryParse(s, out var result) ? result : throw StandardError.Format<PeerChatId>(s);
    public static PeerChatId ParseOrNone(string? s)
        => TryParse(s, out var result) ? result : StandardError.Format<PeerChatId>(s).LogWarning(DefaultLog, None);

    public static bool TryParse(string? s, out PeerChatId result)
    {
        result = default;
        if (s.IsNullOrEmpty())
            return true; // None

        if (!s.OrdinalStartsWith(IdPrefix))
            return false;

        var tail = s.AsSpan(2);
        var userId1Length = tail.IndexOf('-');
        if (userId1Length < 0)
            return false;

        if (!UserId.TryParse(tail[..userId1Length].ToString(), out var userId1) || userId1.IsGuestId)
            return false;
        if (!UserId.TryParse(tail[(userId1Length + 1)..].ToString(), out var userId2) || userId2.IsGuestId)
            return false;
        if (string.CompareOrdinal(userId1.Value, userId2.Value) >= 0)
            return false; // Wrong sort order

        result = new PeerChatId((Symbol)s, userId1, userId2, AssumeValid.Option);
        return true;
    }
}
