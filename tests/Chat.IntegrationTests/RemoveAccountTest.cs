﻿using ActualChat.Testing.Host;
using Stl.Mathematics;

namespace ActualChat.Chat.IntegrationTests;

public class RemoveAccountTest : AppHostTestBase
{
    private ChatId TestChatId { get; } = new("the-actual-one");

    public RemoveAccountTest(ITestOutputHelper @out) : base(@out)
    { }

    [Fact]
    public async Task RemoveOwnEntriesTest()
    {
        using var appHost = await NewAppHost();
        await using var tester = appHost.NewWebClientTester();
        var services = tester.AppServices;
        var bob = await tester.SignIn(new User("Bob"));
        var session = tester.Session;

        var chats = services.GetRequiredService<IChats>();
        var chat = await chats.Get(session, TestChatId, CancellationToken.None);
        chat.Should().NotBeNull();

        var entries = await CreateChatEntries(chats, session, TestChatId, 3);

        var removeEntriesCommand = new ChatsBackend_RemoveOwnEntries(bob.Id);
        await services.Commander().Call(removeEntriesCommand);

        var ids = new HashSet<long>();
        var idTileStack = Constants.Chat.IdTileStack;
        var newEntryRange = new Range<long>(entries.Min(e => e.LocalId), entries.Max(e => e.LocalId) + 1);
        var idTiles = idTileStack.GetOptimalCoveringTiles(newEntryRange);
        foreach (var idTile in idTiles) {
            var tile = await chats.GetTile(session,
                TestChatId,
                ChatEntryKind.Text,
                idTile.Range,
                CancellationToken.None);
            ids.AddRange(tile.Entries.Select(e => e.LocalId));
        }

        foreach (var entry in entries)
            ids.Should().NotContain(entry.LocalId);
    }

    [Fact]
    public async Task RemoveOwnChatsTest()
    {
        using var appHost = await NewAppHost();
        await using var tester = appHost.NewWebClientTester();
        var services = tester.AppServices;
        var bob = await tester.SignIn(new User("Bob"));
        var session = tester.Session;

        var chats = services.GetRequiredService<IChats>();
        var createChatCommand = new Chats_Change(session,
            ChatId.None,
            null,
            new Change<ChatDiff> {
                Create = Option.Some(new ChatDiff {
                    Title = "TestChatToRemove",
                    IsPublic = false,
                    AllowGuestAuthors = true,
                    AllowAnonymousAuthors = true,
                    Kind = ChatKind.Group,
                }),
            });
        var chat = await services.Commander().Call(createChatCommand);
        chat.Should().NotBeNull();

        var entries = await CreateChatEntries(chats, session, chat.Id, 3);
        var removeEntriesCommand = new ChatsBackend_RemoveOwnChats(bob.Id);
        await services.Commander().Call(removeEntriesCommand);

        var chat1 = await chats.Get(session, chat.Id, CancellationToken.None);
        chat1.Should().BeNull();

        var lastEntryLid = entries[^1].Id.LocalId;
        var idTileStack = Constants.Chat.IdTileStack;
        var idTile = idTileStack.GetOptimalCoveringTiles(new Range<long>(lastEntryLid, lastEntryLid))[^1];
        await FluentActions.Awaiting(() => chats.GetTile(session,
                chat.Id,
                ChatEntryKind.Text,
                idTile.Range,
                CancellationToken.None))
            .Should()
            .ThrowAsync<NotFoundException>();
    }


    private async Task<ChatEntry[]> CreateChatEntries(
        IChats chats,
        Session session,
        ChatId chatId,
        int count)
    {
        var phrases = new[] {
            "back in black i hit the sack",
            "rape me rape me my friend",
            "it was a teenage wedding and the all folks wished them well",
        };
        var commander = chats.GetCommander();
        var entries = new List<ChatEntry>();

        while (true)
            foreach (var text in phrases) {
                if (count-- <= 0)
                    return entries.ToArray();

                var command = new Chats_UpsertTextEntry(session, chatId, null, text);
                var entry = await commander.Call(command, CancellationToken.None).ConfigureAwait(false);
                entries.Add(entry);
            }
    }
}
