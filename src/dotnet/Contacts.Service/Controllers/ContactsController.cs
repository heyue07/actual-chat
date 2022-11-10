using Microsoft.AspNetCore.Mvc;
using Stl.Fusion.Server;

namespace ActualChat.Contacts.Controllers;

[Route("api/[controller]/[action]")]
[ApiController, JsonifyErrors, UseDefaultSession]
public class ContactsController : ControllerBase, IContacts
{
    private IContacts Service { get; }
    private ICommander Commander { get; }

    public ContactsController(IContacts service, ICommander commander)
    {
        Service = service;
        Commander = commander;
    }

    [HttpGet, Publish]
    public Task<Contact?> Get(Session session, string id, CancellationToken cancellationToken)
        => Service.Get(session, id, cancellationToken);

    [HttpGet, Publish]
    public Task<Contact?> GetForChat(Session session, string chatId, CancellationToken cancellationToken)
        => Service.GetForChat(session, chatId, cancellationToken);

    [HttpGet, Publish]
    public Task<Contact?> GetForUser(Session session, string userId, CancellationToken cancellationToken)
        => Service.GetForUser(session, userId, cancellationToken);

    [HttpGet, Publish]
    public Task<ImmutableArray<ContactId>> ListIds(
        Session session,
        CancellationToken cancellationToken)
        => Service.ListIds(session, cancellationToken);

    [HttpPost]
    public Task<Contact> Change([FromBody] IContacts.ChangeCommand command, CancellationToken cancellationToken)
        => Commander.Call(command, cancellationToken);

    [HttpPost]
    public Task Touch([FromBody] IContacts.TouchCommand command, CancellationToken cancellationToken)
        => Commander.Call(command, cancellationToken);
}
