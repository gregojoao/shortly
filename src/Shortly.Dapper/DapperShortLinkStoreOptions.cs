namespace Shortly.Dapper;

public sealed class DapperShortLinkStoreOptions
{
    public const string DefaultTableName = "ShortLinks";

    public string TableName { get; set; } = DefaultTableName;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
        {
            throw new InvalidOperationException("DapperShortLinkStoreOptions.TableName is required.");
        }

        foreach (var ch in TableName)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
            {
                throw new InvalidOperationException(
                    $"DapperShortLinkStoreOptions.TableName must only contain letters, digits and underscores (got '{TableName}').");
            }
        }
    }
}
