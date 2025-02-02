namespace ActualChat.Users;

public static class AccountsExt
{
    public static async Task AssertCanRead(
        this IAccounts accounts,
        Session session,
        AccountFull? accessedAccount,
        CancellationToken cancellationToken)
    {
        if (accessedAccount == null)
            return;

        var ownAccount = await accounts.GetOwn(session, cancellationToken).ConfigureAwait(false);
        ownAccount.Require(AccountFull.MustBeActive);
        if (ownAccount.Id != accessedAccount.Id)
            ownAccount.Require(AccountFull.MustBeAdmin);

        throw StandardError.Unauthorized("You can't read accounts of other users.");
    }

    public static async Task AssertCanUpdate(
        this IAccounts accounts,
        Session session,
        AccountFull updatedAccount,
        CancellationToken cancellationToken)
    {
        var ownAccount = await accounts.GetOwn(session, cancellationToken).ConfigureAwait(false);
        ownAccount.Require(AccountFull.MustBeActive);
        if (ownAccount.Id != updatedAccount.Id)
            ownAccount.Require(AccountFull.MustBeAdmin);
        else {
            // User updates its own profile
            if (ownAccount.Phone != updatedAccount.Phone) {
                var verifiedPhoneHash = ownAccount.User.GetPhoneHash();
                if (!verifiedPhoneHash.IsNullOrEmpty() && !OrdinalEquals(updatedAccount.Phone.Value.GetSHA256HashCode(), verifiedPhoneHash))
                    throw StandardError.Unauthorized("You can't change your phone number after it's verified.");
                if (!updatedAccount.Phone.IsValid)
                    throw StandardError.Constraint<Phone>("Incorrect phone number format.");
            }
            if(!OrdinalIgnoreCaseEquals(ownAccount.Email, updatedAccount.Email) && ownAccount.User.HasEmailIdentity())
                throw StandardError.Unauthorized("You can't change your email.");
            if (ownAccount.Status != updatedAccount.Status)
                throw StandardError.Unauthorized("You can't change your own status.");
        }
    }
}
