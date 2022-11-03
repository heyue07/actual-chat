using Microsoft.AspNetCore.Mvc;
using Stl.Fusion.Server;

namespace ActualChat.Invite.Controllers;

[Route("api/[controller]/[action]")]
[ApiController, JsonifyErrors, UseDefaultSession]
public class InvitesController : ControllerBase, IInvites
{
    private IInvites Service { get; }
    private ICommander Commander { get; }

    public InvitesController(IInvites service, ICommander commander)
    {
        Service = service;
        Commander = commander;
    }

    [HttpGet, Publish]
    public Task<ImmutableArray<Invite>> ListUserInvites(Session session, CancellationToken cancellationToken)
        => Service.ListUserInvites(session, cancellationToken);

    [HttpGet, Publish]
    public Task<ImmutableArray<Invite>> ListChatInvites(Session session, string chatId, CancellationToken cancellationToken)
        => Service.ListChatInvites(session, chatId, cancellationToken);

    [HttpPost]
    public Task<Invite> Generate([FromBody] IInvites.GenerateCommand command, CancellationToken cancellationToken)
        => Commander.Call(command, cancellationToken);

    [HttpPost]
    public Task<Invite> Use([FromBody] IInvites.UseCommand command, CancellationToken cancellationToken)
        => Commander.Call(command, cancellationToken);
}
