using Microsoft.EntityFrameworkCore;

namespace Shortly.EntityFrameworkCore;

public static class ShortlyModelBuilderExtensions
{
    public static ModelBuilder AddShortlyModel(
        this ModelBuilder modelBuilder,
        string tableName = ShortLinkRecordConfiguration.DefaultTableName)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new ShortLinkRecordConfiguration(tableName));
        return modelBuilder;
    }
}
