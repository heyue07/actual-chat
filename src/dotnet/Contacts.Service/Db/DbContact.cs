using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stl.Versioning;

namespace ActualChat.Contacts.Db;

[Table("Contacts")]
[Index(nameof(OwnerId))]
public class DbContact : IHasId<string>, IHasVersion<long>, IRequirementTarget
{
    private DateTime _touchedAt;

    [Key] public string Id { get; set; } = null!;
    [ConcurrencyCheck] public long Version { get; set; }

    public string OwnerId { get; set; } = "";
    public string? UserId { get; set; }
    public string? ChatId { get; set; }

    public DateTime TouchedAt {
        get => _touchedAt.DefaultKind(DateTimeKind.Utc);
        set => _touchedAt = value.DefaultKind(DateTimeKind.Utc);
    }

    public DbContact() { }
    public DbContact(Contact contact)
        => UpdateFrom(contact);

    public Contact ToModel()
        => new() {
            Id = Id,
            Version = Version,
            TouchedAt = TouchedAt.ToMoment(),
        };

    public void UpdateFrom(Contact model)
    {
        Version = model.Version;
        TouchedAt = model.TouchedAt.ToDateTimeClamped();
        if (!Id.IsNullOrEmpty())
            return; // Only Version & TouchedAt can be changed for already existing contacts

        var contactId = model.Id.RequireValid();
        Id = contactId;
        OwnerId = contactId.OwnerId;
        switch (contactId.Kind) {
        case ContactKind.User:
            UserId = contactId.OwnerId;
            break;
        case ContactKind.Chat:
            ChatId = contactId.OwnerId;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(model));
        }
    }

    internal class EntityConfiguration : IEntityTypeConfiguration<DbContact>
    {
        public void Configure(EntityTypeBuilder<DbContact> builder)
        {
            builder.Property(a => a.Id).IsRequired();
            builder.Property(a => a.OwnerId).IsRequired();
        }
    }
}


