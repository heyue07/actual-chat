using ActualChat.Hosting;
using Microsoft.Toolkit.HighPerformance;
using Stl.Fusion.Authentication.Commands;

namespace ActualChat.Users.Module;

public partial class UsersDbInitializer
{
    protected override async Task InitializeData(CancellationToken cancellationToken)
    {
        Log.LogInformation("Initializing data...");
        await EnsureAdminExists(cancellationToken).ConfigureAwait(false);
        if (HostInfo.IsDevelopmentInstance && !HostInfo.AppKind.IsTestServer())
            await EnsureTestBotsExist(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureAdminExists(CancellationToken cancellationToken)
    {
        var userId = Constants.User.Admin.UserId;
        var account = await GetInternalAccount(userId, cancellationToken).ConfigureAwait(false);
        if (account != null)
            return;

        Log.LogInformation("Creating admin user...");
        await AddInternalAccount(userId, Constants.User.Admin.Name, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureTestBotsExist(CancellationToken cancellationToken)
    {
        var account = await GetInternalAccount(new UserId("testbot0"), cancellationToken).ConfigureAwait(false);
        if (account != null)
            return;

        Log.LogInformation("Creating test bots...");
        var accounts = await Enumerable
            .Range(0, Constants.User.TestBotCount)
            .Select(async i => {
                var id = new UserId($"testbot{i}");
                var name = $"Robo {RandomNameGenerator.Default.Generate()}";
                Log.LogInformation("+ {UserId}: {UserName}", id, name);
                return await AddInternalAccount(id, name, cancellationToken).ConfigureAwait(false);
            })
            .Collect()
            .ConfigureAwait(false);
        Log.LogInformation("Created {Count} test bots", accounts.Length);
    }

    private async Task<AccountFull?> GetInternalAccount(UserId userId, CancellationToken cancellationToken)
    {
        var accountsBackend = Services.GetRequiredService<IAccountsBackend>();
        var account = await accountsBackend.Get(userId, cancellationToken).ConfigureAwait(false);
        return account;
    }

    private async Task<AccountFull> AddInternalAccount(UserId userId, string name, CancellationToken cancellationToken)
    {
        var commander = Services.Commander();
        var accountsBackend = Services.GetRequiredService<IAccountsBackend>();

        var isAdmin = userId == Constants.User.Admin.UserId;
        var userIdentity = new UserIdentity("internal", userId);

        // Create & sign in the user
        var session = Services.SessionFactory().CreateSession();
        var user = new User(userId, name).WithIdentity(userIdentity);
        var signInCommand = new SignInCommand(session, user, userIdentity);
        await commander.Call(signInCommand, cancellationToken).ConfigureAwait(false);

        // Fetch its account
        var account = await accountsBackend.Get(userId, cancellationToken).ConfigureAwait(false);
        account.Require();
        if (account.Status != AccountStatus.Active) {
            account = account with { Status = AccountStatus.Active };
            var updateCommand = new IAccountsBackend.UpdateCommand(account, account.Version);
            await commander.Call(updateCommand, cancellationToken).ConfigureAwait(false);
            account = await accountsBackend.Get(userId, cancellationToken).ConfigureAwait(false);
        }
        account.Require(isAdmin ? AccountFull.MustBeAdmin : AccountFull.MustBeActive);

        // Create avatar
        var avatarBio = isAdmin ? "Admin" : $"I'm just a {name} test bot";
        var avatarPicture = isAdmin
            ? Constants.User.Admin.Picture
            : $"https://avatars.dicebear.com/api/bottts/{userId.Value.GetDjb2HashCode()}.svg";
        var changeCommand = new IAvatars.ChangeCommand(session, Symbol.Empty, null, new Change<AvatarFull>() {
            Create = new AvatarFull() {
                UserId = account.Id,
                Name = name,
                Bio = avatarBio,
                Picture = avatarPicture,
            },
        });
        var avatar = await commander.Call(changeCommand, cancellationToken).ConfigureAwait(false);

        // Set this avatar as the default one
        var serverKvasBackend = Services.GetRequiredService<IServerKvasBackend>();
        var userKvas = serverKvasBackend.GetUserClient(account);
        var userAvatarSettings = new UserAvatarSettings() {
            DefaultAvatarId = avatar.Id,
            AvatarIds = ImmutableArray.Create(avatar.Id),
        };
        await userKvas.SetUserAvatarSettings(userAvatarSettings, cancellationToken).ConfigureAwait(false);

        // Fetch the final account + do some final checks
        account = await accountsBackend.Get(userId, cancellationToken).ConfigureAwait(false);
        account.Require(isAdmin ? AccountFull.MustBeAdmin : AccountFull.MustBeActive);
        if (account.Status != AccountStatus.Active)
            throw StandardError.Internal("Wrong account status.");
        if (account.Avatar.Require() != avatar)
            throw StandardError.Internal("Wrong avatar.");

        return account;
    }
}
