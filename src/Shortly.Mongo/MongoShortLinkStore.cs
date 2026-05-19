using MongoDB.Driver;
using Shortly.Application.Ports;
using Shortly.Domain;

namespace Shortly.Mongo;

public sealed class MongoShortLinkStore : IShortLinkStore
{
    private readonly IMongoCollection<ShortLinkDocument> _collection;

    public MongoShortLinkStore(IMongoDatabase database, MongoShortLinkStoreOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        options ??= new MongoShortLinkStoreOptions();
        options.Validate();

        _collection = database.GetCollection<ShortLinkDocument>(options.CollectionName);

        if (options.EnsureIndexesOnStartup)
        {
            EnsureIndexes();
        }
    }

    public MongoShortLinkStore(IMongoCollection<ShortLinkDocument> collection, bool ensureIndexes = true)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        if (ensureIndexes)
        {
            EnsureIndexes();
        }
    }

    public async Task<ShortLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var doc = await _collection
            .Find(Builders<ShortLinkDocument>.Filter.Eq(x => x.Slug, slug))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return doc is null ? null : ToDomain(doc);
    }

    public async Task<ShortLink?> FindByTargetAsync(Uri targetUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetUrl);

        var key = targetUrl.AbsoluteUri;
        var doc = await _collection
            .Find(Builders<ShortLinkDocument>.Filter.Eq(x => x.TargetUrl, key))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return doc is null ? null : ToDomain(doc);
    }

    public Task SaveAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        var doc = ToDocument(shortLink);
        return _collection.ReplaceOneAsync(
            Builders<ShortLinkDocument>.Filter.Eq(x => x.Slug, doc.Slug),
            doc,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(string slug, CancellationToken cancellationToken = default)
    {
        var result = await _collection
            .DeleteOneAsync(Builders<ShortLinkDocument>.Filter.Eq(x => x.Slug, slug), cancellationToken)
            .ConfigureAwait(false);

        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    public Task IncrementHitsAsync(string slug, CancellationToken cancellationToken = default)
        => _collection.UpdateOneAsync(
            Builders<ShortLinkDocument>.Filter.Eq(x => x.Slug, slug),
            Builders<ShortLinkDocument>.Update.Inc(x => x.Hits, 1L),
            cancellationToken: cancellationToken);

    private void EnsureIndexes()
    {
        var targetIndex = new CreateIndexModel<ShortLinkDocument>(
            Builders<ShortLinkDocument>.IndexKeys.Ascending(x => x.TargetUrl),
            new CreateIndexOptions { Name = "ix_targetUrl" });

        _collection.Indexes.CreateOne(targetIndex);
    }

    private static ShortLink ToDomain(ShortLinkDocument doc) => new()
    {
        Slug = doc.Slug,
        TargetUrl = new Uri(doc.TargetUrl, UriKind.Absolute),
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        ExpiresAt = doc.ExpiresAt is { } exp
            ? new DateTimeOffset(DateTime.SpecifyKind(exp, DateTimeKind.Utc))
            : null,
        Hits = doc.Hits,
        Metadata = doc.Metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(doc.Metadata, StringComparer.Ordinal)
    };

    private static ShortLinkDocument ToDocument(ShortLink link) => new()
    {
        Slug = link.Slug,
        TargetUrl = link.TargetUrl.AbsoluteUri,
        CreatedAt = link.CreatedAt.UtcDateTime,
        ExpiresAt = link.ExpiresAt?.UtcDateTime,
        Hits = link.Hits,
        Metadata = link.Metadata.Count == 0 ? null : new Dictionary<string, string>(link.Metadata)
    };
}
