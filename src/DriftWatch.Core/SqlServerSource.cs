using Microsoft.Data.SqlClient;

namespace DriftWatch.Core;

/// <summary>
/// Reads schema objects from a SQL Server instance. Strictly read-only:
/// executes a single SELECT against the system catalog views and never
/// issues DDL/DML. The connection string is never logged or exposed —
/// <see cref="Description"/> only reveals server and database.
/// </summary>
public sealed class SqlServerSource : ISchemaSource
{
    private const string Query =
        """
        SELECT s.name AS SchemaName, o.name AS ObjectName, o.type AS TypeCode,
               m.definition AS Definition
        FROM sys.objects o
        JOIN sys.schemas s ON s.schema_id = o.schema_id
        JOIN sys.sql_modules m ON m.object_id = o.object_id
        WHERE o.is_ms_shipped = 0
          AND o.type IN ('V','P','FN','IF','TF','TR')
        ORDER BY s.name, o.name;
        """;

    private readonly string _connectionString;

    public SqlServerSource(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;

        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = string.IsNullOrEmpty(builder.InitialCatalog)
            ? "(default)"
            : builder.InitialCatalog;
        Description = $"SQL Server '{builder.DataSource}', database '{database}'";
    }

    public string Description { get; }

    public async Task<IReadOnlyList<SchemaObject>> ReadAsync(CancellationToken ct) =>
        (await ReadDetailedAsync(ct).ConfigureAwait(false)).Objects;

    /// <summary>
    /// Like <see cref="ReadAsync"/> but also reports objects whose
    /// definition is encrypted. Per-call result, same philosophy as
    /// <see cref="ScriptFolderSource.ReadDetailedAsync"/>.
    /// </summary>
    public async Task<SqlServerReadResult> ReadDetailedAsync(CancellationToken ct)
    {
        var objects = new List<SchemaObject>();
        var encrypted = new List<EncryptedObjectInfo>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = new SqlCommand(Query, connection);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var type = MapTypeCode(reader.GetString(2));

            if (reader.IsDBNull(3))
            {
                encrypted.Add(new EncryptedObjectInfo(schema, name, type));
                continue;
            }

            objects.Add(new SchemaObject(schema, name, type, reader.GetString(3)));
        }

        return new SqlServerReadResult(objects, encrypted);
    }

    /// <summary>
    /// Maps a sys.objects.type code to <see cref="SchemaObjectType"/>.
    /// The column is char(2), so the code arrives space-padded ('V ').
    /// An unknown code throws: the query's WHERE clause only lets the six
    /// known codes through, so anything else means the query and this
    /// mapping are out of sync — silently dropping objects would produce
    /// a false "no drift" result.
    /// </summary>
    internal static SchemaObjectType MapTypeCode(string typeCode) =>
        typeCode.Trim() switch
        {
            "V" => SchemaObjectType.View,
            "P" => SchemaObjectType.StoredProcedure,
            "FN" => SchemaObjectType.ScalarFunction,
            "IF" => SchemaObjectType.TableFunction,
            "TF" => SchemaObjectType.TableFunction,
            "TR" => SchemaObjectType.Trigger,
            var unknown => throw new InvalidOperationException(
                $"Unexpected object type code '{unknown}' returned by the catalog query."),
        };
}
