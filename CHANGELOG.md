# Changelog

All notable changes to **Shortly** will be documented in this file. The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- New satellite package `Shortly.EntityFrameworkCore`: `EfCoreShortLinkStore<TContext>`, `ShortLinkRecord` entity, `IShortlyDbContext` marker, `ModelBuilder.AddShortlyModel()`, and `IServiceCollection.AddShortlyEntityFrameworkCoreStore<TContext>()` that replaces the in-memory default. Uses `ExecuteUpdateAsync` / `ExecuteDeleteAsync` for atomic hit-counter bumps and deletes.
- New satellite package `Shortly.Dapper`: `DapperShortLinkStore` over any `Func<IDbConnection>`, with a registered `DateTimeOffset` type handler for SQLite compatibility and DDL helpers (`DapperShortLinkSql.{Sqlite,Postgres,SqlServer,MySql}`).
- New satellite package `Shortly.Mongo`: `MongoShortLinkStore` over `IMongoDatabase`, BSON document with `[BsonId]` on slug, auto-created `targetUrl` index, atomic `$inc` for hits.
- New satellite package `Shortly.LiteDb`: `LiteDbShortLinkStore` over `ILiteDatabase`, single-file embedded persistence with UTC-ticks date storage to dodge LiteDB's local-time conversion.
- New satellite package `Shortly.StackExchangeRedis`: `IServiceCollection.AddShortlyStackExchangeRedisCache(...)` overloads that register `Microsoft.Extensions.Caching.StackExchangeRedis` and swap the default cache for `DistributedShortLinkCache`.

### Changed
- `IShortlyClient` is now registered as transient (was singleton) so it composes cleanly with scoped storage adapters such as EF Core.

## [0.1.0] - 2026-05-19
### Added
- `IShortlyClient` with `ShortenAsync`, `ResolveAsync` and `DeleteAsync`.
- Pluggable ports: `IShortLinkStore`, `IShortLinkCache`, `ISlugGenerator`.
- In-memory adapters: `InMemoryShortLinkStore`, `InMemoryShortLinkCache`, `NoOpShortLinkCache`.
- `DistributedShortLinkCache` adapter for any `IDistributedCache` (Redis, SQL Server, ...).
- `Base62SlugGenerator` using a human-friendly alphabet (no `0/O/1/I/l`).
- `ShortlyOptions` with validation: `BaseUrl`, `SlugLength`, `CacheTtl`, `DefaultLinkTtl`, `DeduplicateByTarget`, `TrackHits`, `AllowedSchemes`, `AllowedHosts`, `ReservedSlugs`.
- DI registration via `IServiceCollection.AddShortly(...)` with inline-options and `IConfiguration` overloads.
- Typed exception hierarchy rooted at `ShortlyException` (`ShortlyValidationException`, `ShortLinkNotFoundException`, `ShortLinkConflictException`).
- xUnit test suite covering domain validation, options, in-memory adapters, slug generation and client behaviour.
