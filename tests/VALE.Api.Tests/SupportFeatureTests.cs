using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VALE.Api.Areas.PlatformAdmin.Controllers;
using VALE.Api.Configuration;
using VALE.Api.Controllers;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;
using Xunit;

namespace VALE.Api.Tests;

public sealed class SupportFeatureTests
{
    [Fact]
    public async Task Demo_is_idempotent_isolated_and_contains_only_sample_records()
    {
        await using var h = await Harness.CreateAsync();
        await DemoData.EnsureAsync(h.Db, h.Users);
        await DemoData.EnsureAsync(h.Db, h.Users);
        Assert.Single(await h.Db.Companies.Where(x => x.IsDemo).ToListAsync());
        Assert.Equal(3, await h.Db.ParkingTickets.CountAsync(x => x.CompanyId == DemoData.CompanyId));
        Assert.Empty(await h.Db.Customers.Where(x => x.CompanyId == DemoData.CompanyId).ToListAsync());
        var demo = await h.Users.FindByIdAsync(DemoData.UserId.ToString());
        Assert.NotNull(demo);
        Assert.False(await h.Users.HasPasswordAsync(demo));
        var response = await new DemoController(h.Db, h.Users, h.Provider.GetRequiredService<TokenService>()).Start(default);
        var session = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(DemoData.UserId, session.User.Id);
        Assert.Null(session.RefreshToken);
        Assert.True(session.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(60));
    }

    [Theory]
    [InlineData("POST", true, false)]
    [InlineData("PUT", true, false)]
    [InlineData("DELETE", true, false)]
    [InlineData("PATCH", true, false)]
    [InlineData("GET", true, true)]
    [InlineData("POST", false, true)]
    public async Task Demo_write_guard_cannot_be_bypassed_by_screen_choice(string method, bool demo, bool allowed)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var _ = services;
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        if (demo) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("vale_demo", "true")], "test"));
        var nextCalled = false;
        await new DemoReadOnlyMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.Equal(allowed, nextCalled);
        if (!allowed) Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task Backup_preview_rolls_back_then_restore_inserts_once_and_preserves_existing_records()
    {
        await using var h = await Harness.CreateAsync();
        var vehicle = new Vehicle { CompanyId = h.Company.Id, LicensePlate = "34 TEST 01", NormalizedPlate = "34TEST01" };
        h.Db.Vehicles.Add(vehicle);
        await h.Db.SaveChangesAsync();
        var ticket = new ParkingTicket { CompanyId = h.Company.Id, BranchId = h.Branch.Id, VehicleId = vehicle.Id, TicketNumber = "TEST-1", HourlyRate = 25m };
        h.Db.ParkingTickets.Add(ticket);
        await h.Db.SaveChangesAsync();
        var service = new TenantBackupService(h.Db);
        var backup = await service.ExportAsync(h.Company.Id, default);
        Assert.DoesNotContain("PasswordHash", JsonSerializer.Serialize(backup));
        h.Db.ParkingTickets.Remove(ticket); await h.Db.SaveChangesAsync();
        h.Db.ChangeTracker.Clear();
        var preview = await service.RestoreAsync(backup, false, default);
        Assert.Equal(1, preview.Added); Assert.Equal(1, preview.Skipped);
        h.Db.ChangeTracker.Clear();
        Assert.Empty(await h.Db.ParkingTickets.ToListAsync());
        Assert.Equal(1, (await service.RestoreAsync(backup, true, default)).Added);
        h.Db.ChangeTracker.Clear();
        Assert.Equal(0, (await service.RestoreAsync(backup, true, default)).Added);
        Assert.Single(await h.Db.ParkingTickets.ToListAsync());
    }

    [Fact]
    public async Task Backup_rejects_cross_tenant_rows_and_rolls_back_prior_inserts()
    {
        await using var h = await Harness.CreateAsync();
        var customer = new Customer { CompanyId = h.Company.Id, Name = "Örnek Müşteri" };
        var vehicle = new Vehicle { CompanyId = h.Company.Id, LicensePlate = "34 TEST 02", NormalizedPlate = "34TEST02" };
        h.Db.AddRange(customer, vehicle); await h.Db.SaveChangesAsync();
        var service = new TenantBackupService(h.Db);
        var backup = await service.ExportAsync(h.Company.Id, default);
        h.Db.RemoveRange(customer, vehicle); await h.Db.SaveChangesAsync(); h.Db.ChangeTracker.Clear();
        backup.Tables[nameof(Vehicle)][0]["CompanyId"] = JsonSerializer.SerializeToElement(Guid.NewGuid());
        await Assert.ThrowsAsync<ApiException>(() => service.RestoreAsync(backup, true, default));
        h.Db.ChangeTracker.Clear();
        Assert.Empty(await h.Db.Customers.ToListAsync());
        Assert.Empty(await h.Db.Vehicles.ToListAsync());
    }

    [Fact]
    public async Task Profile_optional_fields_round_trip_and_can_be_cleared()
    {
        await using var h = await Harness.CreateAsync();
        var controller = h.Auth();
        var request = new UpdateAccountProfileRequest("Profil Deneme", "+90 555 000 0000", "Light", "Emerald", "#059669", "CarTrack", new DateOnly(1995, 6, 15), "İzmir", "Vale ekibi");
        var response = await controller.UpdateAccountProfile(request, default);
        var profile = Assert.IsType<AccountProfileDto>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(request.BirthDate, profile.BirthDate); Assert.Equal(request.About, profile.About); Assert.Equal(request.City, profile.City);
        var cleared = await controller.UpdateAccountProfile(request with { BirthDate = null, City = null, About = null, PhoneNumber = null }, default);
        profile = Assert.IsType<AccountProfileDto>(Assert.IsType<OkObjectResult>(cleared.Result).Value);
        Assert.Null(profile.BirthDate); Assert.Null(profile.City); Assert.Null(profile.About); Assert.Null(profile.PhoneNumber);
    }

    [Fact]
    public async Task Profile_rejects_future_birth_date_and_invalid_photo()
    {
        await using var h = await Harness.CreateAsync();
        var request = new UpdateAccountProfileRequest("Profil Deneme", null, "Light", "Blue", "#2563EB", "None", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1));
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => h.Auth().UpdateAccountProfile(request, default))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => h.Auth().UpdateProfilePhoto(new("image/jpeg", Convert.ToBase64String(new byte[40])), default))).StatusCode);
    }

    [Fact]
    public async Task Sql_console_fails_closed_without_separate_connection_and_rejects_writes()
    {
        var unconfigured = new DeveloperQueryService(new ConfigurationBuilder().Build());
        Assert.Equal(503, (await Assert.ThrowsAsync<ApiException>(() => unconfigured.QueryAsync("SELECT 1", default))).StatusCode);
        var configured = new DeveloperQueryService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DeveloperTools:ReadOnlyConnectionString"] = "Host=127.0.0.1" }).Build());
        foreach (var sql in new[] { "DELETE FROM \"Companies\"", "SELECT 1; DROP TABLE \"Companies\"", "UPDATE \"Companies\" SET \"Name\" = 'x'" })
            Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => configured.QueryAsync(sql, default))).StatusCode);
    }

    [Fact]
    public void Developer_tools_require_separate_platform_cookie_role_and_antiforgery()
    {
        foreach (var type in new[] { typeof(DatabaseController), typeof(DiagnosticsController) })
        {
            var auth = Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
            Assert.Equal(Roles.PlatformAdmin, auth.Roles);
            Assert.Equal(PlatformAdminSecurity.CookieScheme, auth.AuthenticationSchemes);
            Assert.Single(type.GetCustomAttributes(typeof(AutoValidateAntiforgeryTokenAttribute), true));
        }
    }

    private sealed class Harness(SqliteConnection connection, ServiceProvider provider, Company company, Branch branch, AppUser user) : IAsyncDisposable
    {
        public ServiceProvider Provider => provider;
        public ValeDbContext Db => provider.GetRequiredService<ValeDbContext>();
        public UserManager<AppUser> Users => provider.GetRequiredService<UserManager<AppUser>>();
        public Company Company => company;
        public Branch Branch => branch;
        public AuthController Auth()
        {
            var context = provider.GetRequiredService<IHttpContextAccessor>().HttpContext!;
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", user.Id.ToString()), new("company_id", company.Id.ToString()), new("role", Roles.Owner)], "test", "sub", "role"));
            var controller = ActivatorUtilities.CreateInstance<AuthController>(provider);
            controller.ControllerContext = new ControllerContext { HttpContext = context };
            return controller;
        }
        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging(); services.AddDataProtection();
            services.AddSingleton<IHttpContextAccessor, TestHttpContextAccessor>();
            services.AddDbContext<ValeDbContext>(o => o.UseSqlite(connection));
            services.AddIdentityCore<AppUser>().AddRoles<IdentityRole<Guid>>().AddErrorDescriber<TurkishIdentityErrorDescriber>().AddEntityFrameworkStores<ValeDbContext>().AddDefaultTokenProviders();
            services.AddSingleton<IOptions<JwtOptions>>(Options.Create(new JwtOptions { Key = new string('x', 64) }));
            services.AddSingleton<IOptions<DeviceSessionOptions>>(Options.Create(new DeviceSessionOptions()));
            services.AddSingleton<IOptions<EmailOptions>>(Options.Create(new EmailOptions()));
            services.AddScoped<IValeEmailSender, SmtpValeEmailSender>(); services.AddScoped<CurrentUserContext>(); services.AddScoped<AuditService>();
            services.AddScoped<TokenService>(); services.AddScoped<DeviceSessionService>(); services.AddScoped<PasswordResetCodeService>(); services.AddScoped<OneTimeCodeService>();
            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<ValeDbContext>(); await db.Database.EnsureCreatedAsync();
            var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            foreach (var role in Roles.SeedRoles) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
            var company = new Company { Name = "Support Test", Code = "SUPPORT" };
            var branch = new Branch { Company = company, CompanyId = company.Id, Name = "Merkez", Code = "MRKZ" };
            db.AddRange(company, branch); await db.SaveChangesAsync();
            var user = new AppUser { UserName = "support@example.test", Email = "support@example.test", FullName = "Support User", CompanyId = company.Id, BranchId = branch.Id, Branch = branch, Company = company };
            Assert.True((await provider.GetRequiredService<UserManager<AppUser>>().CreateAsync(user)).Succeeded);
            return new(connection, provider, company, branch, user);
        }
        public async ValueTask DisposeAsync() { await provider.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
