using ActualChat.Commands;
using MemoryPack;

namespace ActualChat.Chat.Events;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public partial record AuthorChangedEvent(
    [property: DataMember, MemoryPackOrder(1)] AuthorFull Author,
    [property: DataMember, MemoryPackOrder(2)] AuthorFull? OldAuthor
) : EventCommand;
