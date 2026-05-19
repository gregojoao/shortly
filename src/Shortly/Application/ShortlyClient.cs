using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shortly.Application.Ports;
using Shortly.Domain;
using Shortly.Exceptions;

namespace Shortly.Application;

public sealed class ShortlyClient : IShortlyClient
{
    private readonly IShortLinkStore _store;
    private readonly IShortLinkCache _cache;
    private readonly ISlugGenerator _slugGenerator;
    private readonly ShortlyOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ShortlyClient> _logger;

    [ActivatorUtilitiesConstructor]
    public ShortlyClient(
        IShortLinkStore store,
        IShortLinkCache cache,
        ISlugGenerator slugGenerator,
        IOptions<ShortlyOptions> options,
        TimeProvider? timeProvider = null,
        ILogger<ShortlyClient>? logger = null)
        : this(
            store,
            cache,
            slugGenerator,
            options?.Value ?? throw new ArgumentNullException(nameof(options)),
            timeProvider,
            logger)
    {
    }

    public ShortlyClient(
        IShortLinkStore store,
        IShortLinkCache cache,
        ISlugGenerator slugGenerator,
        ShortlyOptions options,
        TimeProvider? timeProvider = null,
        ILogger<ShortlyClient>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _slugGenerator = slugGenerator ?? throw new ArgumentNullException(nameof(slugGenerator));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<ShortlyClient>.Instance;
    }

    public async Task<ShortenLinkResult> ShortenAsync(
        ShortenLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureValidTarget(request.TargetUrl);

        var now = _timeProvider.GetUtcNow();

        if (!string.IsNullOrWhiteSpace(request.CustomSlug))
        {
            return await ShortenWithCustomSlugAsync(request, now, cancellationToken).ConfigureAwait(false);
        }

        if (_options.DeduplicateByTarget)
        {
            var existing = await _store.FindByTargetAsync(request.TargetUrl, cancellationToken).ConfigureAwait(false);
            if (existing is not null && !existing.IsExpired(now))
            {
                return BuildResult(existing, alreadyExisted: true);
            }
        }

        var generated = await GenerateUniqueSlugAsync(cancellationToken).ConfigureAwait(false);
        var link = new ShortLink
        {
            Slug = generated,
            TargetUrl = request.TargetUrl,
            CreatedAt = now,
            ExpiresAt = ResolveExpiry(request.Ttl, now),
            Metadata = NormalizeMetadata(request.Metadata)
        };

        await _store.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        await _cache.SetAsync(link, _options.CacheTtl, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Shortly created slug {Slug} for {Target}", link.Slug, link.TargetUrl);

        return BuildResult(link, alreadyExisted: false);
    }

    public async Task<ResolveLinkResult?> ResolveAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSlug(slug);

        var cached = await _cache.GetAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            var now = _timeProvider.GetUtcNow();
            if (cached.IsExpired(now))
            {
                await _cache.RemoveAsync(normalized, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await TrackHitAsync(normalized, cancellationToken).ConfigureAwait(false);
                return BuildResolveResult(cached, servedFromCache: true);
            }
        }

        var stored = await _store.FindBySlugAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return null;
        }

        if (stored.IsExpired(_timeProvider.GetUtcNow()))
        {
            await _store.DeleteAsync(normalized, cancellationToken).ConfigureAwait(false);
            await _cache.RemoveAsync(normalized, cancellationToken).ConfigureAwait(false);
            return null;
        }

        await _cache.SetAsync(stored, _options.CacheTtl, cancellationToken).ConfigureAwait(false);
        await TrackHitAsync(normalized, cancellationToken).ConfigureAwait(false);

        return BuildResolveResult(stored, servedFromCache: false);
    }

    public async Task<bool> DeleteAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSlug(slug);

        await _cache.RemoveAsync(normalized, cancellationToken).ConfigureAwait(false);
        var removed = await _store.DeleteAsync(normalized, cancellationToken).ConfigureAwait(false);

        if (removed)
        {
            _logger.LogDebug("Shortly deleted slug {Slug}", normalized);
        }

        return removed;
    }

    private async Task<ShortenLinkResult> ShortenWithCustomSlugAsync(
        ShortenLinkRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalized = Slug.Normalize(request.CustomSlug!);

        if (!Slug.IsValid(normalized))
        {
            throw new ShortlyValidationException(
                $"Custom slug '{normalized}' is invalid. Use {Slug.MinLength}-{Slug.MaxLength} characters from [A-Za-z0-9_-].");
        }

        if (IsReserved(normalized))
        {
            throw new ShortlyValidationException($"Slug '{normalized}' is reserved.");
        }

        var existing = await _store.FindBySlugAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (existing is not null && !existing.IsExpired(now))
        {
            if (existing.TargetUrl == request.TargetUrl)
            {
                return BuildResult(existing, alreadyExisted: true);
            }

            throw new ShortLinkConflictException(normalized);
        }

        if (existing is not null && existing.IsExpired(now))
        {
            await _store.DeleteAsync(normalized, cancellationToken).ConfigureAwait(false);
            await _cache.RemoveAsync(normalized, cancellationToken).ConfigureAwait(false);
        }

        var link = new ShortLink
        {
            Slug = normalized,
            TargetUrl = request.TargetUrl,
            CreatedAt = now,
            ExpiresAt = ResolveExpiry(request.Ttl, now),
            Metadata = NormalizeMetadata(request.Metadata)
        };

        await _store.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        await _cache.SetAsync(link, _options.CacheTtl, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Shortly created custom slug {Slug} for {Target}", link.Slug, link.TargetUrl);

        return BuildResult(link, alreadyExisted: false);
    }

    private async Task<string> GenerateUniqueSlugAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < _options.MaxSlugGenerationAttempts; attempt++)
        {
            var candidate = _slugGenerator.Generate(_options.SlugLength);

            if (!Slug.IsValid(candidate) || IsReserved(candidate))
            {
                continue;
            }

            var collision = await _store.FindBySlugAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (collision is null)
            {
                return candidate;
            }
        }

        throw new ShortlyException(
            $"Failed to generate a unique slug after {_options.MaxSlugGenerationAttempts} attempts. " +
            $"Increase SlugLength (currently {_options.SlugLength}) or MaxSlugGenerationAttempts.");
    }

    private DateTimeOffset? ResolveExpiry(TimeSpan? requested, DateTimeOffset now)
    {
        var ttl = requested ?? _options.DefaultLinkTtl;
        return ttl is { } value ? now.Add(value) : null;
    }

    private Task TrackHitAsync(string slug, CancellationToken cancellationToken)
        => _options.TrackHits
            ? _store.IncrementHitsAsync(slug, cancellationToken)
            : Task.CompletedTask;

    private bool IsReserved(string slug)
    {
        foreach (var reserved in _options.ReservedSlugs)
        {
            if (string.Equals(reserved, slug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private ShortenLinkResult BuildResult(ShortLink link, bool alreadyExisted) => new()
    {
        Slug = link.Slug,
        ShortUrl = ComposeShortUrl(link.Slug),
        TargetUrl = link.TargetUrl,
        CreatedAt = link.CreatedAt,
        ExpiresAt = link.ExpiresAt,
        AlreadyExisted = alreadyExisted
    };

    private static ResolveLinkResult BuildResolveResult(ShortLink link, bool servedFromCache) => new()
    {
        Slug = link.Slug,
        TargetUrl = link.TargetUrl,
        ExpiresAt = link.ExpiresAt,
        Metadata = link.Metadata,
        ServedFromCache = servedFromCache
    };

    private Uri ComposeShortUrl(string slug)
    {
        var baseUrl = _options.BaseUrl!;
        var basePath = baseUrl.AbsoluteUri.EndsWith('/') ? baseUrl.AbsoluteUri : baseUrl.AbsoluteUri + "/";
        return new Uri(new Uri(basePath, UriKind.Absolute), slug);
    }

    private void EnsureValidTarget(Uri targetUrl)
    {
        if (!ShortlyUrlValidator.IsAcceptable(targetUrl, _options.AllowedSchemes, _options.AllowedHosts))
        {
            throw new ShortlyValidationException(
                $"Target URL '{targetUrl}' is not acceptable. Check AllowedSchemes/AllowedHosts and max length ({ShortlyUrlValidator.MaxUrlLength}).");
        }
    }

    private static string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ShortlyValidationException("Slug must not be empty.");
        }

        var normalized = Slug.Normalize(slug);
        if (!Slug.IsValid(normalized))
        {
            throw new ShortlyValidationException(
                $"Slug '{normalized}' is invalid. Use {Slug.MinLength}-{Slug.MaxLength} characters from [A-Za-z0-9_-].");
        }

        return normalized;
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var clone = new Dictionary<string, string>(metadata.Count, StringComparer.Ordinal);
        foreach (var pair in metadata)
        {
            if (!string.IsNullOrEmpty(pair.Key))
            {
                clone[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        return clone;
    }
}
