using ActualChat.Chat.UI.Blazor.Services;
using ActualChat.UI.Blazor.Services;
using ActualChat.Users;

namespace ActualChat.Chat.IntegrationTests;

public class JsonSerializationOutputTest : TestBase
{
    public JsonSerializationOutputTest(ITestOutputHelper @out) : base(@out) { }

    [Fact]
    public void DumpWarmupJson()
    {
        Dump(new ThemeSettings(Theme.Dark));
        Dump(new UserLanguageSettings() { Primary = Languages.French });
        Dump(new ApiArray<ActiveChat>(new ActiveChat(ChatId.ParseOrNone("dpwo1tm0tw"))));
        Dump(new UserOnboardingSettings() { IsAvatarStepCompleted = true });
        Dump(new ChatListSettings(ChatListOrder.ByAlphabet));
        Dump(new UserBubbleSettings() { ReadBubbles = new ApiArray<string>("x") });
    }

    public void Dump<T>(T instance)
    {
        var s = SystemJsonSerializer.Default;
        Out.WriteLine($"{typeof(T).GetName()}:");
        Out.WriteLine("\"" + s.Write(instance).Replace("\"", "\\\"") + "\"");
        Out.WriteLine("");
    }
}
