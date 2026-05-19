using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Shortly.Application.Ports;
using Shortly.Domain;

namespace Shortly.Dapper;

public sealed class DapperShortLinkStore : IShortLinkStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static int _typeHandlersRegistered;

    private readonly Func<IDbConnection> _connectionFactory;
    private readonly DapperShortLinkStoreOptions _options;

    private readonly string _selectBySlugSql;
    private readonly string _selectByTargetSql;
    private readonly string _insertSql;
    private readonly string _updateSql;
    private readonly string _deleteSql;
    private readonly string _incrementHitsSql;

    public DapperShortLinkStore(
        Func<IDbConnection> connectionFactory,
        DapperShortLinkStoreOptions? options = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = options ?? new DapperShortLinkStoreOptions();
        _options.Validate();

        EnsureTypeHandlersRegistered();

        var table = _options.TableName;
        _selectBySlugSql = $"SELECT Slug, TargetUrl, CreatedAt, ExpiresAt, Hits, MetadataJson FROM {table} WHERE Slug = @Slug";
        _selectByTargetSql = $"SELECT Slug, TargetUrl, CreatedAt, ExpiresAt, Hits, MetadataJson FROM {table} WHERE TargetUrl = @TargetUrl";
        _insertSql = $"INSERT INTO {table} (Slug, TargetUrl, CreatedAt, ExpiresAt, Hits, MetadataJson) VALUES (@Slug, @TargetUrl, @CreatedAt, @ExpiresAt, @Hits, @MetadataJson)";
        _updateSql = $"UPDATE {table} SET TargetUrl = @TargetUrl, CreatedAt = @CreatedAt, ExpiresAt = @ExpiresAt, Hits = @Hits, MetadataJson = @MetadataJson WHERE Slug = @Slug";
        _deleteSql = $"DELETE FROM {table} WHERE Slug = @Slug";
        _incrementHitsSql = $"UPDATE {table} SET Hits = Hits + 1 WHERE Slug = @Slug";
    }

    public async Task<ShortLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        using var connection = OpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ShortLinkRow>(
            new CommandDefinition(_selectBySlugSql, new { Slug = slug }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : ToDomain(row);
    }

    public async Task<ShortLink?> FindByTargetAsync(Uri targetUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetUrl);

        using var connection = OpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ShortLinkRow>(
            new CommandDefinition(_selectByTargetSql, new { TargetUrl = targetUrl.AbsoluteUri }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : ToDomain(row);
    }

    public async Task SaveAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        using var connection = OpenConnection();
        var parameters = ToRow(shortLink);

        var rows = await connection.ExecuteAsync(
            new CommandDefinition(_updateSql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (rows == 0)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(_insertSql, parameters, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteAsync(string slug, CancellationToken cancellationToken = default)
    {
        using var connection = OpenConnection();
        var rows = await connection.ExecuteAsync(
            new CommandDefinition(_deleteSql, new { Slug = slug }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows > 0;
    }

    public async Task IncrementHitsAsync(string slug, CancellationToken cancellationToken = default)
    {
        using var connection = OpenConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(_incrementHitsSql, new { Slug = slug }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private IDbConnection OpenConnection()
    {
        var connection = _connectionFactory()
            ?? throw new InvalidOperationException("DapperShortLinkStore: connection factory returned null.");

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        return connection;
    }

    private static ShortLink ToDomain(ShortLinkRow row) => new()
    {
        Slug = row.Slug,
        TargetUrl = new Uri(row.TargetUrl, UriKind.Absolute),
        CreatedAt = row.CreatedAt,
        ExpiresAt = row.ExpiresAt,
        Hits = row.Hits,
        Metadata = string.IsNullOrEmpty(row.MetadataJson)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : DeserializeMetadata(row.MetadataJson)
    };

    private static object ToRow(ShortLink link) => new
    {
        link.Slug,
        TargetUrl = link.TargetUrl.AbsoluteUri,
        link.CreatedAt,
        link.ExpiresAt,
        link.Hits,
        MetadataJson = link.Metadata.Count == 0
            ? null
            : JsonSerializer.Serialize(link.Metadata, SerializerOptions)
    };

    private static IReadOnlyDictionary<string, string> DeserializeMetadata(string metadataJson)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, SerializerOptions);
        return raw is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(raw, StringComparer.Ordinal);
    }

    private static void EnsureTypeHandlersRegistered()
    {
        if (Interlocked.CompareExchange(ref _typeHandlersRegistered, 1, 0) != 0)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
    }

    private sealed class ShortLinkRow
    {
        public string Slug { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public long Hits { get; set; }
        public string? MetadataJson { get; set; }
    }

    private sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => dt.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero)
                : new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero),
            string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            _ => throw new InvalidCastException($"Cannot convert {value?.GetType().FullName ?? "null"} to DateTimeOffset.")
        };

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.Value = value;
        }
    }
}
