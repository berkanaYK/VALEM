using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using Xunit;

namespace VALE.Api.Tests;

public sealed class DeviceSessionServiceTests
{
    [Fact]
    public async Task Create_stores_only_a_hash_and_rotation_rejects_replay()
    {
        await using var harness = await DeviceSessionHarness.CreateAsync();

        var issued = await harness.Service.CreateAsync(harness.User, "Berkan test telefonu", default);
        var stored = await harness.Db.DeviceSessions.AsNoTracking().SingleAsync();

        Assert.NotEqual(issued.Token, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal("Berkan test telefonu", stored.DeviceName);

        var rotated = await harness.Service.RotateAsync(issued.Token, "Berkan test telefonu", default);
        Assert.NotNull(rotated);
        Assert.NotEqual(issued.Token, rotated.Token);
        Assert.Null(await harness.Service.RotateAsync(issued.Token, null, default));

        var sessions = (await harness.Db.DeviceSessions.AsNoTracking().ToListAsync())
            .OrderBy(x => x.CreatedAt)
            .ToList();
        Assert.Equal(2, sessions.Count);
        Assert.NotNull(sessions[0].RevokedAt);
        Assert.Null(sessions[1].RevokedAt);
    }

    [Fact]
    public async Task Revocation_and_security_stamp_change_invalidate_a_session()
    {
        await using var harness = await DeviceSessionHarness.CreateAsync();
        var revoked = await harness.Service.CreateAsync(harness.User, null, default);

        await harness.Service.RevokeAsync(revoked.Token, default);
        Assert.Null(await harness.Service.RotateAsync(revoked.Token, null, default));

        var stale = await harness.Service.CreateAsync(harness.User, null, default);
        harness.User.SecurityStamp = Guid.NewGuid().ToString("N");
        await harness.Db.SaveChangesAsync();

        Assert.Null(await harness.Service.RotateAsync(stale.Token, null, default));
    }

    private sealed class DeviceSessionHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private DeviceSessionHarness(SqliteConnection connection, ValeDbContext db, AppUser user)
        {
            _connection = connection;
            Db = db;
            User = user;
            Service = new DeviceSessionService(db, Options.Create(new DeviceSessionOptions { LifetimeDays = 30 }));
        }

        public ValeDbContext Db { get; }
        public AppUser User { get; }
        public DeviceSessionService Service { get; }

        public static async Task<DeviceSessionHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ValeDbContext>().UseSqlite(connection).Options;
            var db = new ValeDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var company = new Company { Name = "Test Vale", Code = $"TEST-{Guid.NewGuid():N}" };
            var branch = new Branch
            {
                Company = company,
                CompanyId = company.Id,
                Name = "Merkez",
                Code = "MRKZ",
                City = "Antalya"
            };
            var user = new AppUser
            {
                UserName = "session@example.test",
                Email = "session@example.test",
                EmailConfirmed = true,
                FullName = "Session Test",
                Company = company,
                CompanyId = company.Id,
                Branch = branch,
                BranchId = branch.Id,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                IsActive = true
            };
            db.AddRange(company, branch, user);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            user = await db.Users.Include(x => x.Company).Include(x => x.Branch).SingleAsync(x => x.Id == user.Id);
            return new DeviceSessionHarness(connection, db, user);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
