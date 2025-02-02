using ActualChat.Chat;
using ActualChat.Invite.Backend;
using ActualChat.Invite.Db;
using ActualChat.Commands;
using ActualChat.Kvas;
using ActualChat.Users;
using Microsoft.EntityFrameworkCore;
using Stl.Fusion.EntityFramework;

namespace ActualChat.Invite;

internal class InvitesBackend : DbServiceBase<InviteDbContext>, IInvitesBackend
{
    private IAccounts? _accounts;
    private IChatsBackend? _chatsBackend;
    private IServerKvas? _serverKvas;

    private IAccounts Accounts => _accounts ??= Services.GetRequiredService<IAccounts>();
    private IChatsBackend ChatsBackend => _chatsBackend ??= Services.GetRequiredService<IChatsBackend>();
    private IServerKvas ServerKvas => _serverKvas ??= Services.GetRequiredService<IServerKvas>();
    private IDbEntityResolver<string, DbInvite> DbInviteResolver { get; }
    private IDbEntityResolver<string, DbActivationKey> DbActivationKeyResolver { get; }

    public InvitesBackend(IServiceProvider services) : base(services)
    {
        DbInviteResolver = services.GetRequiredService<IDbEntityResolver<string, DbInvite>>();
        DbActivationKeyResolver = services.GetRequiredService<IDbEntityResolver<string, DbActivationKey>>();
    }

    // [ComputeMethod]
    public virtual async Task<Invite?> Get(string id, CancellationToken cancellationToken)
    {
        var dbInvite = await DbInviteResolver.Get(id, cancellationToken).ConfigureAwait(false);
        return dbInvite?.ToModel();
    }

    // [ComputeMethod]
    public virtual async Task<ApiArray<Invite>> GetAll(string searchKey, int minRemaining, CancellationToken cancellationToken)
    {
        await PseudoGetAll(searchKey).ConfigureAwait(false);

        var dbContext = CreateDbContext();
        await using var _ = dbContext.ConfigureAwait(false);

        var dbInvites = await dbContext.Invites
            .Where(x => x.SearchKey == searchKey && x.Remaining >= minRemaining)
            .OrderByDescending(x => x.ExpiresOn)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return dbInvites.Select(x => x.ToModel()).ToApiArray();
    }

    // [ComputeMethod]
    public virtual async Task<bool> IsValid(string activationKey, CancellationToken cancellationToken)
    {
        var dbActivationKey = await DbActivationKeyResolver.Get(activationKey, cancellationToken).ConfigureAwait(false);
        return dbActivationKey != null;
    }

    // [CommandHandler]
    public virtual async Task<Invite> OnGenerate(
        InvitesBackend_Generate command,
        CancellationToken cancellationToken)
    {
        var context = CommandContext.GetCurrent();

        if (Computed.IsInvalidating()) {
            var invInvite = context.Operation().Items.Get<Invite>();
            if (invInvite != null) {
                _ = PseudoGetAll(invInvite.Details?.GetSearchKey() ?? "");
                _ = Get(invInvite.Id, default);
            }
            return default!;
        }

        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var __ = dbContext.ConfigureAwait(false);

        var expiresOn = command.Invite.ExpiresOn;
        if (expiresOn == default)
            expiresOn = Clocks.SystemClock.Now + Constants.Invites.Defaults.ExpiresIn;
        var invite = command.Invite with {
            Id = DbInvite.IdGenerator.Next(),
            Version = VersionGenerator.NextVersion(),
            CreatedAt = Clocks.SystemClock.Now,
            ExpiresOn = expiresOn,
        };
        dbContext.Invites.Add(new DbInvite(invite));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        context.Operation().Items.Set(invite);
        return invite;
    }

    // [CommandHandler]
    public virtual async Task<Invite> OnUse(
        InvitesBackend_Use command,
        CancellationToken cancellationToken)
    {
        var context = CommandContext.GetCurrent();

        if (Computed.IsInvalidating()) {
            var invInvite = context.Operation().Items.Get<Invite>();
            if (invInvite != null) {
                _ = PseudoGetAll(invInvite.Details?.GetSearchKey() ?? "");
                _ = Get(invInvite.Id, default);
            }
            return default!;
        }

        var session = command.Session;
        var account = await Accounts.GetOwn(command.Session, cancellationToken).ConfigureAwait(false);

        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var __ = dbContext.ConfigureAwait(false);

        var dbInvite = await dbContext.Invites
                .FirstOrDefaultAsync(x => x.Id == command.InviteId, cancellationToken)
                .ConfigureAwait(false)
            ?? throw StandardError.NotFound<Invite>("Invite with the specified code is not found.");

        var invite = dbInvite.ToModel();
        invite = invite.Use(VersionGenerator);

        switch (invite.Details.Option) {
        case UserInviteOption:
            if (account.IsGuestOrNone)
                throw StandardError.Unauthorized("Please sign in and open this link again to use this invite.");
            if (account.Status == AccountStatus.Suspended)
                throw StandardError.Unauthorized("A suspended account cannot be re-activated via invite code.");
            if (account.IsActive())
                throw StandardError.StateTransition("Your account is already active.");

            // Follow-up actions
            new AccountsBackend_Update(account with { Status = AccountStatus.Active }, null)
                .EnqueueOnCompletion();
            break;
        case ChatInviteOption chatInviteOption:
            var chatId = chatInviteOption.ChatId;
            _ = await ChatsBackend.Get(chatId, cancellationToken).Require().ConfigureAwait(false);

            var dbActivationKey = new DbActivationKey(invite.Id);
            dbContext.Add(dbActivationKey);
            context.Operation().Items.Set(dbActivationKey.Id);

            await ServerKvas.GetClient(session)
                .Set(ServerKvasInviteKey.ForChat(chatId), dbActivationKey.Id, cancellationToken)
                .ConfigureAwait(false);
            break;
        default:
            throw StandardError.Format<Invite>();
        }
        dbInvite.UpdateFrom(invite);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        context.Operation().Items.Set(invite);
        return invite;
    }

    // [CommandHandler]
    public virtual async Task OnRevoke(
        InvitesBackend_Revoke command,
        CancellationToken cancellationToken)
    {
        var context = CommandContext.GetCurrent();

        if (Computed.IsInvalidating()) {
            var invInvite = context.Operation().Items.Get<Invite>();
            if (invInvite != null) {
                _ = PseudoGetAll(invInvite.Details?.GetSearchKey() ?? "");
                _ = Get(invInvite.Id, default);
            }
            return;
        }

        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var __ = dbContext.ConfigureAwait(false);

        var dbInvite = await dbContext.Invites
                .FirstOrDefaultAsync(x => x.Id == command.InviteId, cancellationToken)
                .ConfigureAwait(false)
            ?? throw StandardError.NotFound<Invite>("Invite with the specified code is not found.");

        var invite = dbInvite.ToModel();
        invite = invite.Revoke(VersionGenerator);
        dbInvite.UpdateFrom(invite);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        context.Operation().Items.Set(invite);
    }

    [ComputeMethod]
    protected virtual Task<Unit> PseudoGetAll(string searchKey)
        => Stl.Async.TaskExt.UnitTask;
}
