namespace Shortly.LiteDb;

public sealed class LiteDbShortLinkStoreOptions
{
    public const string DefaultCollectionName = "shortLinks";

    public string CollectionName { get; set; } = DefaultCollectionName;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CollectionName))
        {
            throw new InvalidOperationException("LiteDbShortLinkStoreOptions.CollectionName is required.");
        }
    }
}
