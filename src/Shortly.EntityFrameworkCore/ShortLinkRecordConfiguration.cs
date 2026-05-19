using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shortly.Domain;

namespace Shortly.EntityFrameworkCore;

public sealed class ShortLinkRecordConfiguration : IEntityTypeConfiguration<ShortLinkRecord>
{
    public const string DefaultTableName = "ShortLinks";

    private readonly string _tableName;

    public ShortLinkRecordConfiguration(string tableName = DefaultTableName)
    {
        _tableName = string.IsNullOrWhiteSpace(tableName)
            ? throw new ArgumentException("Table name must be provided.", nameof(tableName))
            : tableName;
    }

    public void Configure(EntityTypeBuilder<ShortLinkRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(_tableName);

        builder.HasKey(x => x.Slug);

        builder.Property(x => x.Slug)
            .HasMaxLength(Slug.MaxLength)
            .IsRequired();

        builder.Property(x => x.TargetUrl)
            .HasMaxLength(ShortlyUrlValidator.MaxUrlLength)
            .IsRequired();

        builder.HasIndex(x => x.TargetUrl)
            .HasDatabaseName("IX_ShortLinks_TargetUrl");

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ExpiresAt);
        builder.Property(x => x.Hits).IsRequired();
        builder.Property(x => x.MetadataJson);
    }
}
