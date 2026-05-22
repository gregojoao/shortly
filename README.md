# Shortly

>Lightweight, self-hosted URL shortener SDK for .NET. Bring your own persistence and cache, plug it into any .NET worker/API/Telegram bot and own the short links end-to-end.

[![CI](https://github.com/gregojoao/shortly/actions/workflows/ci.yml/badge.svg)](https://github.com/gregojoao/shortly/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Shortly.svg?label=nuget&color=004880)](https://www.nuget.org/packages/Shortly)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%2010-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)

- **Pluggable**: ports for storage (`IShortLinkStore`), cache (`IShortLinkCache`) and slug generation (`ISlugGenerator`).
- **Batteries-included**: in-memory adapters for dev/tests plus an `IDistributedCache` adapter that fits Redis, SQL Server, NCache, etc.
- **Drop-in adapters**: official satellite packages for **Entity Framework Core** (Postgres, SQL Server, SQLite, MySQL, ...) and **StackExchange.Redis**.
- **Deterministic slugs**: Base62 alphabet with the visually-ambiguous characters (`0`, `O`, `1`, `I`, `l`) removed.
- **Safe by default**: validates target URLs, enforces an allow-list of schemes/hosts, and refuses reserved slugs (`admin`, `api`, `health`, ...).
- **Cache-friendly**: short link rows are immutable once written, so the SDK uses a 24-hour cache TTL by default and proactively invalidates on `DeleteAsync`.
- Targets **.NET 8** and **.NET 10**.

---

## Packages

| Package                          | Purpose                                                                                |
|----------------------------------|----------------------------------------------------------------------------------------|
| `Shortly`                        | Core SDK. Ports, in-memory adapters, options, validation, exceptions. Zero DB deps.    |
| `Shortly.EntityFrameworkCore`    | `IShortLinkStore` over the consumer's `DbContext`. Postgres/SQL Server/SQLite/MySQL.   |
| `Shortly.Dapper`                 | `IShortLinkStore` over any ADO.NET provider via Dapper. DDL helpers for the main SQL dialects. |
| `Shortly.Mongo`                  | `IShortLinkStore` over `IMongoDatabase`. Atomic `$inc` for the hit counter.            |
| `Shortly.LiteDb`                 | `IShortLinkStore` over an embedded LiteDB single-file database — zero ops, perfect for solo deployments. |
| `Shortly.StackExchangeRedis`     | `IShortLinkCache` over Redis via `Microsoft.Extensions.Caching.StackExchangeRedis`.    |

Importing only `Shortly` gives you a working, in-memory shortener with no third-party transitive dependencies. Add a satellite for production.

## Install

```bash
dotnet add package Shortly
dotnet add package Shortly.EntityFrameworkCore     # ORM-based SQL
dotnet add package Shortly.Dapper                  # raw SQL
dotnet add package Shortly.Mongo                   # MongoDB
dotnet add package Shortly.LiteDb                  # embedded single-file
dotnet add package Shortly.StackExchangeRedis      # Redis cache
```

## Quick start

### 1. Configure

`appsettings.json`:

```json
{
  "Shortly": {
    "BaseUrl": "https://l.example.com",
    "SlugLength": 7,
    "CacheTtl": "1.00:00:00",
    "DefaultLinkTtl": "180.00:00:00",
    "DeduplicateByTarget": true,
    "AllowedHosts": [ "shopee.com.br", "*.shopee.com.br", "aliexpress.com", "*.aliexpress.com" ]
  }
}
```

### 2. Register

```csharp
using Shortly.Infrastructure.DependencyInjection;

builder.Services.AddShortly(builder.Configuration);
```

Or inline:

```csharp
builder.Services.AddShortly(options =>
{
    options.BaseUrl = new Uri("https://l.example.com");
    options.SlugLength = 7;
    options.CacheTtl = TimeSpan.FromHours(24);
    options.DefaultLinkTtl = TimeSpan.FromDays(180);
});
```

### 3. Use

```csharp
public sealed class PromoService
{
    private readonly IShortlyClient _shortly;

    public PromoService(IShortlyClient shortly) => _shortly = shortly;

    public async Task<string> ShortenForBotAsync(Uri offerUrl, CancellationToken ct)
    {
        var result = await _shortly.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = offerUrl,
            Ttl = TimeSpan.FromDays(30),
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "telegram-bot",
                ["campaign"] = "black-friday"
            }
        }, ct);

        return result.ShortUrl.AbsoluteUri;
    }
}
```

On your hosted redirect app you resolve and 302-forward:

```csharp
app.MapGet("/{slug}", async (string slug, IShortlyClient shortly, CancellationToken ct) =>
{
    var resolved = await shortly.ResolveAsync(slug, ct);
    return resolved is null
        ? Results.NotFound()
        : Results.Redirect(resolved.TargetUrl.AbsoluteUri, permanent: false);
});
```

---

## Hosting

The SDK is in-process. To make `https://l.example.com/{slug}` work you host whatever app you like — minimal API, ASP.NET Core, Azure Functions, AWS Lambda — and call `IShortlyClient.ResolveAsync` from the redirect handler. The same SDK and the same store/cache are also used by the producer side (in this repo's case, the Telegram promo bot).

Recommended deployment:

| Component         | Recommended                                                          |
|-------------------|----------------------------------------------------------------------|
| **Storage**       | PostgreSQL/SQL Server/SQLite via [`Shortly.EntityFrameworkCore`](#postgres-sql-server-sqlite--shortlyentityframeworkcore). |
| **Cache**         | Redis via [`Shortly.StackExchangeRedis`](#redis--shortlystackexchangeredis). |
| **Redirect host** | Minimal API behind Cloudflare/Caddy at your short domain.            |
| **Producer**      | Inject `IShortlyClient` directly into your bot/API workers.          |

### Postgres / SQL Server / SQLite — `Shortly.EntityFrameworkCore`

Add `ShortLinkRecord` to a `DbContext` that you already own:

```csharp
public sealed class AppDbContext : DbContext, IShortlyDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ShortLinkRecord> ShortLinks => Set<ShortLinkRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddShortlyModel(); // creates the ShortLinks table + indexes
    }
}
```

Wire it up:

```csharp
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddShortly(builder.Configuration);
builder.Services.AddShortlyEntityFrameworkCoreStore<AppDbContext>();
```

The store uses EF Core's `ExecuteUpdateAsync`/`ExecuteDeleteAsync` for the hit counter and deletes, so they're atomic single-statement operations. Metadata is stored as JSON in a single column — switch to `jsonb` on Postgres by adding `entity.Property(x => x.MetadataJson).HasColumnType("jsonb")` in your fluent configuration if you want indexable JSON.

Generate the migration:

```bash
dotnet ef migrations add AddShortlyTables --context AppDbContext
dotnet ef database update --context AppDbContext
```

### Raw SQL — `Shortly.Dapper`

For users who want SQL without an ORM. The store wraps a `Func<IDbConnection>` factory, so it works with Npgsql, Microsoft.Data.SqlClient, Microsoft.Data.Sqlite, MySqlConnector, etc.

```csharp
builder.Services.AddShortly(builder.Configuration);
builder.Services.AddShortlyDapperStore(
    sp => new NpgsqlConnection(builder.Configuration.GetConnectionString("Default")),
    o => o.TableName = "ShortLinks");
```

The factory must return a **new** `IDbConnection` per call — the store opens, uses, and disposes each connection. `DapperShortLinkSql` exposes DDL strings for Postgres, SQL Server, SQLite and MySQL:

```csharp
await connection.ExecuteAsync(DapperShortLinkSql.Postgres());
```

A Dapper `TypeHandler` for `DateTimeOffset` is registered on first construction so SQLite (which stores datetimes as TEXT) round-trips correctly without extra setup.

### MongoDB — `Shortly.Mongo`

```csharp
builder.Services.AddShortly(builder.Configuration);
builder.Services.AddShortlyMongoStore(
    connectionString: builder.Configuration.GetConnectionString("Mongo")!,
    databaseName: "shortly");
```

The store auto-creates a unique index on the `_id` (slug) plus an ascending index on `targetUrl`. Hit counts use `$inc` for native atomicity. The driver pulls in `MongoDB.Driver`, which currently has open transitive CVE advisories for `SharpCompress` and `Snappier` — these are tracked by the MongoDB team and out of our control; the project suppresses `NU1902`/`NU1903` so the build remains clean. If you must avoid those advisories entirely, prefer EF Core or LiteDB.

### Embedded — `Shortly.LiteDb`

Single-file, in-process, zero external infra — great for self-hosted small deployments or for shipping a turnkey container.

```csharp
builder.Services.AddShortly(builder.Configuration);
builder.Services.AddShortlyLiteDbStore("Filename=/var/shortly/links.db;Connection=shared");
```

`Slug` is mapped as the BSON `_id` and `targetUrl` gets an automatic index. Dates are stored as UTC ticks (`long`) so they round-trip across time zones with zero ambiguity.

### Redis — `Shortly.StackExchangeRedis`

```csharp
builder.Services.AddShortly(builder.Configuration);
builder.Services.AddShortlyStackExchangeRedisCache(
    builder.Configuration.GetConnectionString("Redis")!,
    instanceName: "shortly:");
```

This single call registers `IDistributedCache` (the StackExchange.Redis impl) and replaces the default in-memory `IShortLinkCache` with `DistributedShortLinkCache`. Existing `IDistributedCache` users can also wire `DistributedShortLinkCache` manually — `AddShortlyStackExchangeRedisCache` is just a convenience.

---

## Architecture

The SDK follows a small DDD-inspired layout:

| Layer          | What lives there                                                                                          |
|----------------|-----------------------------------------------------------------------------------------------------------|
| Domain         | `ShortLink`, `Slug`, `ShortlyUrlValidator` — pure rules.                                                  |
| Application    | `IShortlyClient`, `ShortlyClient`, `ShortlyOptions`, requests/results, and ports.                         |
| Infrastructure | In-memory store, in-memory + distributed cache adapters, Base62 slug generator, DI registration.          |
| Exceptions     | Typed hierarchy rooted at `ShortlyException`.                                                             |

### Ports

```csharp
public interface IShortLinkStore
{
    Task<ShortLink?> FindBySlugAsync(string slug, CancellationToken ct = default);
    Task<ShortLink?> FindByTargetAsync(Uri targetUrl, CancellationToken ct = default);
    Task SaveAsync(ShortLink shortLink, CancellationToken ct = default);
    Task<bool> DeleteAsync(string slug, CancellationToken ct = default);
    Task IncrementHitsAsync(string slug, CancellationToken ct = default);
}

public interface IShortLinkCache
{
    Task<ShortLink?> GetAsync(string slug, CancellationToken ct = default);
    Task SetAsync(ShortLink shortLink, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string slug, CancellationToken ct = default);
}

public interface ISlugGenerator
{
    string Generate(int length);
}
```

`AddShortly(...)` registers the in-memory defaults with `TryAddSingleton`, so providing your own implementation is a one-liner *after* the call. The satellite packages (`Shortly.EntityFrameworkCore`, `Shortly.StackExchangeRedis`) use `RemoveAll<T>` + `Add` so they cleanly replace the defaults regardless of call order. Hand-rolled overrides:

```csharp
builder.Services.AddShortly(builder.Configuration);

// Custom port implementations.
builder.Services.AddSingleton<IShortLinkStore, MyCustomStore>();
builder.Services.AddSingleton<ISlugGenerator, MyVanitySlugGenerator>();
```

---

## Options reference

| Option                       | Default                                          | Notes                                                                                   |
|------------------------------|--------------------------------------------------|-----------------------------------------------------------------------------------------|
| `BaseUrl`                    | *required*                                       | The public base of your short links, e.g. `https://l.example.com`.                       |
| `SlugLength`                 | `7`                                              | 7 chars over a 57-symbol alphabet ≈ 1.6×10¹² combinations. Increase for very large catalogs. |
| `MaxSlugGenerationAttempts`  | `5`                                              | Retries before giving up on a slug collision.                                            |
| `CacheTtl`                   | `24h`                                            | Slugs are immutable, so this can be much longer in practice — 24h is a safe default.     |
| `DefaultLinkTtl`             | `null` (never expires)                           | Lifetime for new links when the request omits `Ttl`.                                     |
| `DeduplicateByTarget`        | `true`                                           | Reuses the existing slug if the target URL was already shortened.                        |
| `TrackHits`                  | `true`                                           | Calls `IncrementHitsAsync` on every successful resolve.                                  |
| `AllowedSchemes`             | `http`, `https`                                  | Schemes accepted for the target URL.                                                     |
| `AllowedHosts`               | empty (allow all)                                | Optional host allow-list. Supports `*.suffix.tld` wildcards.                             |
| `ReservedSlugs`              | `admin`, `api`, `health`, `robots.txt`, `favicon.ico` | Slugs the SDK refuses to mint (useful when your redirect app exposes admin endpoints).   |

### Cache TTL — why 24 hours?

Short links never change after creation: a slug always points to the same target. So the cache TTL only matters as a safety net (e.g. honoring deletes that propagated through a different instance). 24 hours strikes a good balance: it absorbs read storms, keeps storage cold paths fast, and bounds staleness if the cache and the store are ever inconsistent. Bump it to 7 days or more if your traffic is read-heavy and you can rely on `IShortlyClient.DeleteAsync` for invalidations.

---

## Releases & NuGet publishing

The repo is a **monorepo** — one solution, six packable projects. Each `.csproj` carries its own `<Version>` element and ships as an independent NuGet package.

### Workflows

| File                                                  | Trigger                       | What it does                                                                        |
|-------------------------------------------------------|-------------------------------|-------------------------------------------------------------------------------------|
| [`.github/workflows/ci.yml`](.github/workflows/ci.yml)                       | push / PR to `main`/`master`  | `restore` → `test` → `pack` (no push). Catches build regressions per commit.        |
| [`.github/workflows/publish-nuget.yml`](.github/workflows/publish-nuget.yml) | `workflow_dispatch` (manual)  | Same as CI, plus `dotnet nuget push` of every `.nupkg`/`.snupkg` with `--skip-duplicate`. |

### One-time setup

1. Generate an API key at <https://www.nuget.org/account/apikeys> scoped to **Push new packages and package versions** for the package prefix `Shortly*`.
2. In the GitHub repo: **Settings → Secrets and variables → Actions → New repository secret**. Name it `NUGET_API_KEY`, paste the value.

### Releasing a single package

1. Bump `<Version>` on the project you want to release (e.g. `src/Shortly.Mongo/Shortly.Mongo.csproj` from `0.1.0` to `0.1.1`).
2. Update the matching entry in [`CHANGELOG.md`](CHANGELOG.md).
3. Commit and push.
4. Go to **Actions → Publish NuGet → Run workflow**.

The job packs all six projects but `--skip-duplicate` makes NuGet ignore versions that already exist. So only the one you actually bumped is published; the other five are silent no-ops. No per-package workflows to maintain.

### Releasing all packages together

Bump every project's `<Version>` to the same number (handy when there's a coordinated change in the core that the satellites need to depend on). Run the workflow once; all six go out.

### Versioning policy

Each package follows [SemVer](https://semver.org/). Satellite packages depend on `Shortly` via `<ProjectReference>` during dev, but the generated `.nuspec` resolves that to a real package dependency at the `<Version>` of `Shortly` at pack time. So if you bump `Shortly` to `0.2.0` and want satellites to require that floor, bump them as well.

---

## License

MIT © 2026 Greco Labs. See [LICENSE](./LICENSE).
# Test PR workflow

This line exists only to exercise pull request approvals.
