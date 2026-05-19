using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Shortly.Application.Ports;
using Shortly.Domain;

namespace Shortly.EntityFrameworkCore;

public sealed class EfCoreShortLinkStore<TContext> : IShortLinkStore
    where TContext : DbContext, IShortlyDbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly TContext _context;

    public EfCoreShortLinkStore(TContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ShortLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var record = await _context.ShortLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : ToDomain(record);
    }

    public async Task<ShortLink?> FindByTargetAsync(Uri targetUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetUrl);

        var key = targetUrl.AbsoluteUri;
        var record = await _context.ShortLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TargetUrl == key, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : ToDomain(record);
    }

    public async Task SaveAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        var tracked = await _context.ShortLinks
            .FirstOrDefaultAsync(x => x.Slug == shortLink.Slug, cancellationToken)
            .ConfigureAwait(false);

        if (tracked is null)
        {
            _context.ShortLinks.Add(ToRecord(shortLink));
        }
        else
        {
            tracked.TargetUrl = shortLink.TargetUrl.AbsoluteUri;
            tracked.CreatedAt = shortLink.CreatedAt;
            tracked.ExpiresAt = shortLink.ExpiresAt;
            tracked.Hits = shortLink.Hits;
            tracked.MetadataJson = SerializeMetadata(shortLink.Metadata);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(string slug, CancellationToken cancellationToken = default)
    {
        var affected = await _context.ShortLinks
            .Where(x => x.Slug == slug)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return affected > 0;
    }

    public Task IncrementHitsAsync(string slug, CancellationToken cancellationToken = default)
        => _context.ShortLinks
            .Where(x => x.Slug == slug)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Hits, x => x.Hits + 1), cancellationToken);

    private static ShortLink ToDomain(ShortLinkRecord record) => new()
    {
        Slug = record.Slug,
        TargetUrl = new Uri(record.TargetUrl, UriKind.Absolute),
        CreatedAt = record.CreatedAt,
        ExpiresAt = record.ExpiresAt,
        Hits = record.Hits,
        Metadata = DeserializeMetadata(record.MetadataJson)
    };

    private static ShortLinkRecord ToRecord(ShortLink link) => new()
    {
        Slug = link.Slug,
        TargetUrl = link.TargetUrl.AbsoluteUri,
        CreatedAt = link.CreatedAt,
        ExpiresAt = link.ExpiresAt,
        Hits = link.Hits,
        MetadataJson = SerializeMetadata(link.Metadata)
    };

    private static string? SerializeMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }

    private static IReadOnlyDictionary<string, string> DeserializeMetadata(string? metadataJson)
    {
        if (string.IsNullOrEmpty(metadataJson))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, SerializerOptions);
        return raw is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(raw, StringComparer.Ordinal);
    }
}
