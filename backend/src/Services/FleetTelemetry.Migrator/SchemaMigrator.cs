using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FleetTelemetry.Migrator;

/// <summary>
/// Aplica los scripts SQL versionados que definen el esquema.
/// </summary>
/// <remarks>
/// Se usan scripts y no EF Migrations porque el esquema depende de funciones de TimescaleDB
/// —<c>create_hypertable</c>, políticas de compresión y retención— que el modelo de EF no sabe
/// expresar. Mezclar ambos mecanismos dejaría media verdad en las migraciones y la otra media en un
/// script suelto. Ver docs/adr/0004.
/// </remarks>
internal sealed partial class SchemaMigrator(string connectionString, ILogger<SchemaMigrator> logger)
{
    private const string MigrationTableSql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version     VARCHAR(128) PRIMARY KEY,
            applied_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        """;

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, MigrationTableSql, cancellationToken).ConfigureAwait(false);

        var applied = await LoadAppliedVersionsAsync(connection, cancellationToken).ConfigureAwait(false);

        foreach (var (version, sql) in LoadScripts())
        {
            if (applied.Contains(version))
            {
                MigrationSkipped(logger, version);
                continue;
            }

            // Cada script va en su propia transacción: si el segundo falla, el primero sigue
            // aplicado y registrado, y el reintento arranca donde se quedó.
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await ExecuteAsync(connection, sql, cancellationToken, transaction).ConfigureAwait(false);

                await ExecuteAsync(
                    connection,
                    "INSERT INTO schema_migrations (version) VALUES (@version);",
                    cancellationToken,
                    transaction,
                    new NpgsqlParameter("version", version)).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                MigrationApplied(logger, version);
            }
            catch (PostgresException exception)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                MigrationFailed(logger, exception, version);
                throw;
            }
        }
    }

    /// <summary>Scripts embebidos, ordenados por su prefijo numérico.</summary>
    private static IEnumerable<(string Version, string Sql)> LoadScripts()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var names = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);

            var version = name.Split('.')[^2];
            yield return (version, reader.ReadToEnd());
        }
    }

    private static async Task<HashSet<string>> LoadAppliedVersionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);

        await using var command = new NpgsqlCommand("SELECT version FROM schema_migrations;", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null,
        params NpgsqlParameter[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Migración {Version} aplicada.")]
    static partial void MigrationApplied(ILogger logger, string version);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Debug, Message = "Migración {Version} ya estaba aplicada.")]
    static partial void MigrationSkipped(ILogger logger, string version);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error, Message = "Falló la migración {Version}.")]
    static partial void MigrationFailed(ILogger logger, Exception exception, string version);
}
