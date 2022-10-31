namespace ActualChat;

public static class DefaultUserPicture
{
    public const int DefaultSize = 160;

    public static string Get(User user, int size = DefaultSize)
        => GetGravatar(user, size) ?? GetAvataaar(user.Name.GetMD5HashCode());

    public static string Get(User? user, string hash, int size = DefaultSize)
        => GetGravatar(user, size) ?? GetAvataaar(hash);

    public static string GetAvataaar(string hash)
        => $"https://avatars.dicebear.com/api/avataaars/{hash.UrlEncode()}.svg";

    public static string? GetGravatar(User? user, int size = DefaultSize)
        => GetGravatar(GetEmail(user), size);

    public static string? GetGravatar(string? email, int size = DefaultSize)
    {
        if (email.IsNullOrEmpty())
            return null;

        var hash = email.GetMD5HashCode().ToLowerInvariant();
        return $"https://www.gravatar.com/avatar/{hash}?s={size}";
    }

    private static string? GetEmail(User? user)
    {
        var emailClaim = user?.Claims.GetValueOrDefault(System.Security.Claims.ClaimTypes.Email);
        var email = emailClaim?.Trim().NullIfEmpty();
        return email;
    }
}
