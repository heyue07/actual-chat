namespace ActualChat.Users;

[DataContract]
public sealed record UserOnboardingSettings
{
    public const string KvasKey = nameof(UserOnboardingSettings);

    [DataMember] public bool IsPhoneStepCompleted { get; init; }
    [DataMember] public bool IsAvatarStepCompleted { get; init; }
    [DataMember] public DateTime? LastShownAt { get; init; }

    public bool ShouldBeShown() {
        if (LastShownAt.HasValue && DateTime.UtcNow < LastShownAt.Value.AddDays(1))
            return false;

        if (!IsPhoneStepCompleted)
            return true;

        if (!IsAvatarStepCompleted)
            return true;

        return false;
    }
}
