using ActualChat.Kvas;

namespace ActualChat.Users;

public static class UserChatSettingsExt
{
    public static async ValueTask<LanguageId> LanguageOrPrimary(
        this UserChatSettings userChatSettings, IKvas kvas,
        CancellationToken cancellationToken = default)
    {
        var language = userChatSettings.Language;
        if (language.IsValid)
            return language;

        var userLanguageSettings = await kvas.GetUserLanguageSettings(cancellationToken).ConfigureAwait(false);
        return userLanguageSettings.Primary;
    }
}
