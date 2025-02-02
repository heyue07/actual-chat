using ActualChat.Kvas;

namespace ActualChat.Users;

public static class ServerKvasBackendExt
{
    public static IKvas GetUserClient(this IServerKvasBackend serverKvasBackend, User user)
        => serverKvasBackend.GetUserClient(new UserId(user.Id));

    public static IKvas GetUserClient(this IServerKvasBackend serverKvasBackend, Account account)
        => serverKvasBackend.GetUserClient(account.Id);

    public static IKvas GetUserClient(this IServerKvasBackend serverKvasBackend, UserId userId)
        => new ServerKvasBackendClient(serverKvasBackend, GetUserPrefix(userId));

    public static string GetUserPrefix(UserId userId)
        => userId.IsNone
            ? ""
            : userId.IsGuest
                ? $"g/{userId}/"
                : $"u/{userId}/";
}
