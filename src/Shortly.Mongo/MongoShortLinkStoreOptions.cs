namespace Shortly.Mongo;

public sealed class MongoShortLinkStoreOptions
{
    public const string DefaultCollectionName = "shortLinks";

    public string CollectionName { get; set; } = DefaultCollectionName;

    public bool EnsureIndexesOnStartup { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CollectionName))
        {
            throw new InvalidOperationException("MongoShortLinkStoreOptions.CollectionName is required.");
        }
    }
}
