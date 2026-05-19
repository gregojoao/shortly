using LiteDB;
using Shortly.Application.Ports;
using Shortly.Domain;

namespace Shortly.LiteDb;

public sealed class LiteDbShortLinkStore : IShortLinkStore
{
    private readonly ILiteDatabase _database;
    private readonly LiteDbShortLinkStoreOptions _options;
    private readonly object _writeLock = new();

    public LiteDbShortLinkStore(ILiteDatabase database, LiteDbShortLinkStoreOptions? options = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _options = options ?? new LiteDbShortLinkStoreOptions();
        _options.Validate();

        var collection = GetCollection();
        collection.EnsureIndex(x => x.TargetUrl);
    }

    public Task<ShortLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var doc = GetCollection().FindById(slug);
        return Task.FromResult<ShortLink?>(doc is null ? null : ToDomain(doc));
    }

    public Task<ShortLink?> FindByTargetAsync(Uri targetUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetUrl);

        var key = targetUrl.AbsoluteUri;
        var doc = GetCollection().FindOne(x => x.TargetUrl == key);
        return Task.FromResult<ShortLink?>(doc is null ? null : ToDomain(doc));
    }

    public Task SaveAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        lock (_writeLock)
        {
            GetCollection().Upsert(ToDocument(shortLink));
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string slug, CancellationToken cancellationToken = default)
    {
        bool removed;
        lock (_writeLock)
        {
            removed = GetCollection().Delete(slug);
        }
        return Task.FromResult(removed);
    }

    public Task IncrementHitsAsync(string slug, CancellationToken cancellationToken = default)
    {
        lock (_writeLock)
        {
            var collection = GetCollection();
            var doc = collection.FindById(slug);
            if (doc is null)
            {
                return Task.CompletedTask;
            }

            doc.Hits += 1;
            collection.Update(doc);
        }

        return Task.CompletedTask;
    }

    private ILiteCollection<ShortLinkDocument> GetCollection()
    {
        var collection = _database.GetCollection<ShortLinkDocument>(_options.CollectionName);
        return collection;
    }

    private static ShortLink ToDomain(ShortLinkDocument doc) => new()
    {
        Slug = doc.Slug,
        TargetUrl = new Uri(doc.TargetUrl, UriKind.Absolute),
        CreatedAt = new DateTimeOffset(doc.CreatedAtTicks, TimeSpan.Zero),
        ExpiresAt = doc.ExpiresAtTicks is { } ticks
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
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
        CreatedAtTicks = link.CreatedAt.UtcTicks,
        ExpiresAtTicks = link.ExpiresAt?.UtcTicks,
        Hits = link.Hits,
        Metadata = link.Metadata.Count == 0 ? null : new Dictionary<string, string>(link.Metadata)
    };
}
