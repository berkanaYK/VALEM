using System.Data;
using Npgsql;

namespace VALE.Api.Services;

public sealed record DeveloperQueryResult(string[] Columns, List<string?[]> Rows, bool Truncated);

public sealed class DeveloperQueryService(IConfiguration configuration)
{
    public bool Enabled => !string.IsNullOrWhiteSpace(configuration["DeveloperTools:ReadOnlyConnectionString"]);

    public async Task<DeveloperQueryResult> QueryAsync(string sql, CancellationToken ct)
    {
        if (!Enabled) throw new ApiException(503, "Sorgu bağlantısı ayarlanmadı", "Sunucuda DeveloperTools:ReadOnlyConnectionString alanına yalnız okuma yetkili veritabanı hesabını tanımlayın.");
        sql = sql.Trim().TrimEnd(';').Trim();
        if (sql.Length is < 6 or > 8000 || sql.Contains(';') ||
            !(sql.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase) || sql.StartsWith("SELECT\n", StringComparison.OrdinalIgnoreCase) || sql.StartsWith("WITH ", StringComparison.OrdinalIgnoreCase)))
            throw new ApiException(400, "Sorgu uygun değil", "Tek bir SELECT veya WITH sorgusu yazın. En fazla 200 satır gösterilir.");

        var connectionOptions = new NpgsqlConnectionStringBuilder(configuration["DeveloperTools:ReadOnlyConnectionString"]!)
        { Pooling = false, Timeout = 5, CommandTimeout = 5, ApplicationName = "VALEM Developer Console" };
        await using var connection = new NpgsqlConnection(connectionOptions.ConnectionString);
        await connection.OpenAsync(ct);
        // The console refuses powerful roles, including inherited write/DDL privileges.
        const string privilegeCheck = """
            SELECT r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls
              OR has_database_privilege(current_user, current_database(), 'CREATE')
              OR EXISTS (SELECT 1 FROM pg_namespace n WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname <> 'information_schema' AND has_schema_privilege(current_user, n.oid, 'CREATE'))
              OR EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                         WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname <> 'information_schema'
                         AND (pg_has_role(current_user, c.relowner, 'USAGE') OR has_table_privilege(current_user, c.oid, 'INSERT,UPDATE,DELETE,TRUNCATE,TRIGGER')))
              OR EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                         WHERE p.prosecdef AND n.nspname NOT LIKE 'pg_%' AND has_function_privilege(current_user, p.oid, 'EXECUTE'))
            FROM pg_roles r WHERE r.rolname = current_user
            """;
        await using (var check = new NpgsqlCommand(privilegeCheck, connection))
            if (await check.ExecuteScalarAsync(ct) is not false)
                throw new ApiException(503, "Sorgu hesabının yetkileri uygun değil", "Sorgu ekranı için ayrı, yalnız okuma yetkili ve tablo sahibi olmayan bir hesap kullanın.");

        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await using (var limits = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '5s'; SET LOCAL lock_timeout = '1s';", connection, transaction))
            await limits.ExecuteNonQueryAsync(ct);
        await using var command = new NpgsqlCommand($"SELECT * FROM ({sql}) AS vale_preview LIMIT 201", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (reader.FieldCount > 50) throw new ApiException(400, "Çok fazla sütun", "Sorguda en fazla 50 sütun seçin.");
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<string?[]>();
        while (await reader.ReadAsync(ct) && rows.Count < 201)
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : Format(reader.GetValue(i))).ToArray());
        var truncated = rows.Count > 200;
        if (truncated) rows.RemoveAt(200);
        return new(columns, rows, truncated);
    }
    private static string Format(object value)
    {
        if (value is byte[] bytes) return $"İkili veri ({bytes.Length} bayt)";
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        return text.Length > 2000 ? text[..2000] + "…" : text;
    }
}
