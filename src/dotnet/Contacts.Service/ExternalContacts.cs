﻿using ActualChat.Users;

namespace ActualChat.Contacts;

public class ExternalContacts(IServiceProvider services) : IExternalContacts
{
    private IAccounts Accounts { get; } = services.GetRequiredService<IAccounts>();
    private IExternalContactsBackend Backend { get; } = services.GetRequiredService<IExternalContactsBackend>();
    private ICommander Commander { get; } = services.GetRequiredService<ICommander>();

    // [ComputeMethod]
    public virtual async Task<ApiArray<ExternalContact>> List(Session session, Symbol deviceId, CancellationToken cancellationToken)
    {
        var account = await Accounts.GetOwn(session, cancellationToken).ConfigureAwait(false);
        if (!account.IsActive())
            return default;

        return await Backend.List(account.Id, deviceId, cancellationToken).ConfigureAwait(false);
    }

    // TODO: bulk change?
    // [CommandHandler]
    public virtual async Task<ExternalContact?> OnChange(ExternalContacts_Change command, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return default!; // It just spawns other commands, so nothing to do here

        var (session, id, expectedVersion, change) = command;
        var account = await Accounts.GetOwn(session, cancellationToken).ConfigureAwait(false);
        if (!account.IsActive())
            return null;

        id.Require();
        change.RequireValid();

        if (id.OwnerId != account.Id)
            throw Unauthorized();

        return await Commander
            .Call(new ExternalContactsBackend_Change(id, expectedVersion, change), cancellationToken)
            .ConfigureAwait(false);
    }

    private static Exception Unauthorized()
        => StandardError.Unauthorized("You can access only your own external contacts.");
}
