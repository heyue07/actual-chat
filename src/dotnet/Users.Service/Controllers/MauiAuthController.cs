using Microsoft.AspNetCore.Mvc;
using Stl.Fusion.Server.Authentication;

namespace ActualChat.Users.Controllers;

// NOTE(AY): All requests to this controller must be opened in browser to rather than called via RestEase!
[Route(Route)]
public sealed class MauiAuthController : Controller
{
    public const string Route = "/maui-auth";

    private ServerAuthHelper? _serverAuthHelper;
    private UrlMapper? _urlMapper;
    private ILogger? _log;

    private IServiceProvider Services { get; }
    private ServerAuthHelper ServerAuthHelper => _serverAuthHelper ??= Services.GetRequiredService<ServerAuthHelper>();
    private UrlMapper UrlMapper => _urlMapper ??= Services.GetRequiredService<UrlMapper>();
    private ILogger Log => _log ??= Services.LogFor(GetType());

    public MauiAuthController(IServiceProvider services)
        => Services = services;

    [HttpGet("sign-in/{scheme}")]
    public ActionResult SignIn(
        string scheme, [FromQuery(Name = "s")] string sessionToken, string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var session = new Session(sessionToken).RequireValid();
        if (returnUrl.IsNullOrEmpty())
            returnUrl = UrlMapper.ToAbsolute(Links.AutoClose("Sign-in"));
        var syncUrl = UrlMapper.ToAbsolute(
            $"{Route}/sync?s={session.Id.UrlEncode()}&returnUrl={returnUrl.UrlEncode()}");
        return Redirect($"/signIn/{scheme}?returnUrl={syncUrl.UrlEncode()}");
    }

    [HttpGet("sign-out")]
    public ActionResult SignOut(
        [FromQuery(Name = "s")] string sessionToken, string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var session = new Session(sessionToken).RequireValid();
        if (returnUrl.IsNullOrEmpty())
            returnUrl = UrlMapper.ToAbsolute(Links.AutoClose("Sign-out"));
        var syncUrl = UrlMapper.ToAbsolute(
            $"{Route}/sync?s={session.Id.UrlEncode()}&returnUrl={returnUrl.UrlEncode()}");
        return Redirect($"/signOut?returnUrl={syncUrl.UrlEncode()}");
    }

    [HttpGet("sync")]
    public async Task<ActionResult> Sync(
        [FromQuery(Name = "s")] string sessionToken, string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var session = new Session(sessionToken).RequireValid();
        await ServerAuthHelper.UpdateAuthState(session, HttpContext, cancellationToken).ConfigureAwait(false);
        returnUrl = returnUrl.NullIfEmpty() ?? Links.AutoClose("Authentication state update").Value;
        return Redirect(returnUrl);
    }
}
