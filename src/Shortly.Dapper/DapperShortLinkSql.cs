namespace Shortly.Dapper;

public static class DapperShortLinkSql
{
    public static string Sqlite(string tableName = "ShortLinks") =>
        $"""
        CREATE TABLE IF NOT EXISTS {tableName} (
            Slug          TEXT     PRIMARY KEY,
            TargetUrl     TEXT     NOT NULL,
            CreatedAt     TEXT     NOT NULL,
            ExpiresAt     TEXT     NULL,
            Hits          INTEGER  NOT NULL DEFAULT 0,
            MetadataJson  TEXT     NULL
        );
        CREATE INDEX IF NOT EXISTS IX_{tableName}_TargetUrl ON {tableName}(TargetUrl);
        """;

    public static string Postgres(string tableName = "ShortLinks") =>
        $"""
        CREATE TABLE IF NOT EXISTS {tableName} (
            "Slug"          varchar(64)   PRIMARY KEY,
            "TargetUrl"     varchar(2048) NOT NULL,
            "CreatedAt"     timestamptz   NOT NULL,
            "ExpiresAt"     timestamptz   NULL,
            "Hits"          bigint        NOT NULL DEFAULT 0,
            "MetadataJson"  text          NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_{tableName}_TargetUrl" ON {tableName}("TargetUrl");
        """;

    public static string SqlServer(string tableName = "ShortLinks") =>
        $"""
        IF OBJECT_ID(N'{tableName}', N'U') IS NULL
        BEGIN
            CREATE TABLE [{tableName}] (
                [Slug]          NVARCHAR(64)   NOT NULL PRIMARY KEY,
                [TargetUrl]     NVARCHAR(2048) NOT NULL,
                [CreatedAt]     DATETIMEOFFSET NOT NULL,
                [ExpiresAt]     DATETIMEOFFSET NULL,
                [Hits]          BIGINT         NOT NULL CONSTRAINT [DF_{tableName}_Hits] DEFAULT (0),
                [MetadataJson]  NVARCHAR(MAX)  NULL
            );
            CREATE INDEX [IX_{tableName}_TargetUrl] ON [{tableName}]([TargetUrl]);
        END
        """;

    public static string MySql(string tableName = "ShortLinks") =>
        $"""
        CREATE TABLE IF NOT EXISTS `{tableName}` (
            `Slug`          VARCHAR(64)   NOT NULL,
            `TargetUrl`     VARCHAR(2048) NOT NULL,
            `CreatedAt`     DATETIME(6)   NOT NULL,
            `ExpiresAt`     DATETIME(6)   NULL,
            `Hits`          BIGINT        NOT NULL DEFAULT 0,
            `MetadataJson`  TEXT          NULL,
            PRIMARY KEY (`Slug`),
            INDEX `IX_{tableName}_TargetUrl` (`TargetUrl`(255))
        );
        """;
}
