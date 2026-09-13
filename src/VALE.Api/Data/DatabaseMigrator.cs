using System.Data;
using Microsoft.EntityFrameworkCore;

namespace VALE.Api.Data;

public static class DatabaseMigrator
{
    public const string BaselineMigrationId = "20260831115011_InitialVersionedSchema";
    private const long MigrationLockId = 8_617_203_200_001;

    private static readonly string[] BaselineTables =
    [
        "AspNetRoles", "AspNetUsers", "AspNetRoleClaims", "AspNetUserClaims", "AspNetUserLogins",
        "AspNetUserRoles", "AspNetUserTokens", "Companies", "Branches", "UserBranchMemberships",
        "RegistrationRequests", "Notifications", "PushRegistrations", "Customers", "Vehicles",
        "ParkingTickets", "Payments", "AuditEntries"
    ];

    private static readonly (string Table, string Column)[] RequiredLegacyColumns =
    [
        ("AspNetUsers", "CompanyId"), ("AspNetUsers", "BranchId"), ("AspNetUsers", "EmployeeCode"),
        ("Branches", "CompanyId"), ("Branches", "InviteCode"),
        ("Vehicles", "CompanyId"), ("Vehicles", "PhotoBase64"),
        ("ParkingTickets", "CompanyId"), ("ParkingTickets", "DeletedAt"), ("ParkingTickets", "UpdatedAt"),
        ("Payments", "CompanyId"), ("AuditEntries", "CompanyId")
    ];

    public static async Task MigrateAsync(
        ValeDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(db.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            throw new InvalidOperationException("VALEM üretim migration sistemi PostgreSQL/Npgsql sağlayıcısı gerektirir.");

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await ExecuteScalarAsync(db, $"SELECT pg_advisory_lock({MigrationLockId});", cancellationToken);
            await AdoptVerifiedLegacySchemaAsync(db, logger, cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            if (pending.Length > 0)
                throw new InvalidOperationException($"Uygulanamayan veritabanı migration'ları var: {string.Join(", ", pending)}");
            logger.LogInformation("EF Core migration'ları doğrulandı; veritabanı şeması güncel.");
        }
        finally
        {
            try { await ExecuteScalarAsync(db, $"SELECT pg_advisory_unlock({MigrationLockId});", CancellationToken.None); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    private static async Task AdoptVerifiedLegacySchemaAsync(
        ValeDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var hasMigrationHistory = await TableExistsAsync(db, "__EFMigrationsHistory", cancellationToken);
        var applied = hasMigrationHistory
            ? (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal)
            : [];
        if (applied.Contains(BaselineMigrationId)) return;

        var hasUsers = await TableExistsAsync(db, "AspNetUsers", cancellationToken);
        var hasAnyValeTable = hasUsers || await TableExistsAsync(db, "Companies", cancellationToken) ||
            await TableExistsAsync(db, "Branches", cancellationToken) ||
            await TableExistsAsync(db, "ParkingTickets", cancellationToken);
        if (!hasAnyValeTable)
        {
            await EnsureMigrationHistoryTableAsync(db, cancellationToken);
            return;
        }
        if (!hasUsers)
            throw new InvalidOperationException("Veritabanı kısmen oluşturulmuş görünüyor; AspNetUsers tablosu yok. Otomatik migration güvenlik amacıyla durduruldu.");

        var missingTables = new List<string>();
        foreach (var table in BaselineTables)
        {
            if (!await TableExistsAsync(db, table, cancellationToken)) missingTables.Add(table);
        }

        var missingColumns = new List<string>();
        foreach (var (table, column) in RequiredLegacyColumns)
        {
            if (!await ColumnExistsAsync(db, table, column, cancellationToken)) missingColumns.Add($"{table}.{column}");
        }

        if (missingTables.Count > 0 || missingColumns.Count > 0)
        {
            var details = string.Join("; ", new[]
            {
                missingTables.Count == 0 ? null : $"eksik tablolar: {string.Join(", ", missingTables)}",
                missingColumns.Count == 0 ? null : $"eksik alanlar: {string.Join(", ", missingColumns)}"
            }.Where(x => x is not null));
            throw new InvalidOperationException($"Eski VALEM şeması başlangıç migration'ı olarak güvenle doğrulanamadı ({details}). Veri kaybını önlemek için işlem durduruldu.");
        }

        await EnsureMigrationHistoryTableAsync(db, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20260831115011_InitialVersionedSchema', '10.0.4')
            ON CONFLICT ("MigrationId") DO NOTHING;
            """,
            cancellationToken);
        logger.LogInformation("Doğrulanmış VALEM 3.1.2 şeması EF Core başlangıç migration'ına güvenle bağlandı.");
    }

    private static Task EnsureMigrationHistoryTableAsync(ValeDbContext db, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """,
            cancellationToken);

    private static async Task<bool> TableExistsAsync(ValeDbContext db, string table, CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(
            db,
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @name);",
            cancellationToken,
            table);
        return result is true;
    }

    private static async Task<bool> ColumnExistsAsync(ValeDbContext db, string table, string column, CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table AND column_name = @column);";
        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "table";
        tableParameter.Value = table;
        command.Parameters.Add(tableParameter);
        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "column";
        columnParameter.Value = column;
        command.Parameters.Add(columnParameter);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<object?> ExecuteScalarAsync(
        ValeDbContext db,
        string sql,
        CancellationToken cancellationToken,
        string? name = null)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        if (name is not null)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "name";
            parameter.Value = name;
            command.Parameters.Add(parameter);
        }
        if (command.Connection?.State != ConnectionState.Open)
            throw new InvalidOperationException("Migration veritabanı bağlantısı açık değil.");
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
