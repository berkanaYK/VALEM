using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed record TenantBackup(int FormatVersion, Guid CompanyId, string CompanyName, DateTimeOffset ExportedAt,
    Dictionary<string, List<Dictionary<string, JsonElement>>> Tables);
public sealed record RestoreResult(int Added, int Skipped);

public sealed class TenantBackupService(ValeDbContext db)
{
    private static readonly Type[] Types = [typeof(Customer), typeof(Vehicle), typeof(ParkingTicket), typeof(Payment)];
    private const int MaxRows = 5000;

    public async Task<TenantBackup> ExportAsync(Guid companyId, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == companyId && !x.IsDemo, ct)
            ?? throw new ApiException(400, "Firma bulunamadı", "Dışa aktarılacak gerçek bir firma seçin.");
        await using var transaction = await db.Database.BeginTransactionAsync(
            db.Database.IsNpgsql() ? System.Data.IsolationLevel.RepeatableRead : System.Data.IsolationLevel.Serializable, ct);
        var tables = new Dictionary<string, List<Dictionary<string, JsonElement>>>();
        tables[nameof(Customer)] = Rows(await db.Customers.AsNoTracking().Where(x => x.CompanyId == companyId).Take(MaxRows + 1).ToListAsync(ct));
        tables[nameof(Vehicle)] = Rows(await db.Vehicles.AsNoTracking().Where(x => x.CompanyId == companyId).Take(MaxRows + 1).ToListAsync(ct));
        tables[nameof(ParkingTicket)] = Rows(await db.ParkingTickets.IgnoreQueryFilters().AsNoTracking().Where(x => x.CompanyId == companyId).Take(MaxRows + 1).ToListAsync(ct));
        tables[nameof(Payment)] = Rows(await db.Payments.IgnoreQueryFilters().AsNoTracking().Where(x => x.CompanyId == companyId).Take(MaxRows + 1).ToListAsync(ct));
        await transaction.CommitAsync(ct);
        return new(1, companyId, company.Name, DateTimeOffset.UtcNow, tables);
    }

    private List<Dictionary<string, JsonElement>> Rows<T>(List<T> entities) where T : class
    {
        if (entities.Count > MaxRows) throw new ApiException(400, "Dosya sınırı", "Bu firma için sunucu üzerinden tam yedek alın. Mobil aktarım tablo başına 5.000 kayıtla sınırlıdır.");
        var properties = db.Model.FindEntityType(typeof(T))!.GetProperties().ToArray();
        return entities.Select(entity => properties.ToDictionary(p => p.Name,
            p => JsonSerializer.SerializeToElement(p.PropertyInfo!.GetValue(entity), p.ClrType))).ToList();
    }

    public async Task<RestoreResult> RestoreAsync(TenantBackup backup, bool commit, CancellationToken ct)
    {
        if (backup.FormatVersion != 1 || backup.Tables is null || backup.Tables.Count != Types.Length ||
            Types.Any(t => !backup.Tables.ContainsKey(t.Name)) || backup.Tables.Values.Any(rows => rows is null || rows.Count > MaxRows))
            throw InvalidBackup();
        if (!await db.Companies.AnyAsync(x => x.Id == backup.CompanyId && !x.IsDemo, ct)) throw InvalidBackup();
        var added = 0; var skipped = 0;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var type in Types)
        {
            var metadata = db.Model.FindEntityType(type)!;
            var properties = metadata.GetProperties().ToArray();
            var ids = new HashSet<Guid>();
            foreach (var row in backup.Tables[type.Name])
            {
                if (row is null || row.Count != properties.Length || properties.Any(p => !row.ContainsKey(p.Name))) throw InvalidBackup();
                var entity = Activator.CreateInstance(type)!;
                foreach (var property in properties)
                {
                    var value = row[property.Name].Deserialize(property.ClrType);
                    if (value is null && !property.IsNullable) throw InvalidBackup();
                    if (value is string text && property.GetMaxLength() is { } limit && text.Length > limit) throw InvalidBackup();
                    property.PropertyInfo!.SetValue(entity, value);
                }
                var id = (Guid)metadata.FindProperty("Id")!.PropertyInfo!.GetValue(entity)!;
                var tenantId = (Guid)metadata.FindProperty("CompanyId")!.PropertyInfo!.GetValue(entity)!;
                if (id == Guid.Empty || !ids.Add(id) || tenantId != backup.CompanyId) throw InvalidBackup();
                if (entity is ParkingTicket ticket && (ticket.HourlyRate <= 0 || ticket.AmountDue < 0 || ticket.PaidAmount < 0 || !Enum.IsDefined(ticket.Status))) throw InvalidBackup();
                if (entity is Payment payment && (payment.Amount <= 0 || !Enum.IsDefined(payment.Method))) throw InvalidBackup();
                var existing = await db.FindAsync(type, [id], ct);
                if (existing is not null)
                {
                    if ((Guid)metadata.FindProperty("CompanyId")!.PropertyInfo!.GetValue(existing)! != backup.CompanyId) throw InvalidBackup();
                    skipped++; continue;
                }
                db.Add(entity); added++;
            }
            // Dependencies are inserted first; all tables share the transaction.
            await db.SaveChangesAsync(ct);
        }
        if (commit) await transaction.CommitAsync(ct); else await transaction.RollbackAsync(ct);
        return new(added, skipped);
    }
    private static ApiException InvalidBackup() => new(400, "Yedek doğrulanamadı", "Dosya biçimi, firma kimliği veya kayıt ilişkileri uygun değil. Aynı firmadan alınmış özgün VALEM aktarım dosyasını kullanın.");
}
