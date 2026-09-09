using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Controllers;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;
using Xunit;

namespace VALE.Api.Tests;

public sealed class SelfServiceRegistrationTests
{
    [Fact]
    public async Task Owner_registration_is_active_without_invite_or_approval()
    {
        await using var harness = await RegistrationHarness.CreateAsync();

        var response = await harness.Controller.RegisterOwner(new OwnerRegisterRequest(
            FullName: "Deneme Yönetici",
            Email: "owner-self-service@example.test",
            Password: null,
            PhoneNumber: null,
            CompanyName: "Rastgele Firma",
            LoginMethod: LoginMethods.EmailCode), default);

        var created = Assert.IsType<CreatedResult>(response.Result);
        var result = Assert.IsType<RegisterResponse>(created.Value);
        Assert.False(result.RequiresApproval);

        var user = await harness.Users.FindByEmailAsync("owner-self-service@example.test");
        Assert.NotNull(user);
        Assert.True(user.IsActive);
        Assert.False(await harness.Users.HasPasswordAsync(user));
        Assert.Contains(Roles.Owner, await harness.Users.GetRolesAsync(user));
        Assert.NotNull(user.CompanyId);
        Assert.NotNull(user.BranchId);
    }

    [Fact]
    public async Task Staff_registration_creates_an_active_isolated_company_with_unique_codes()
    {
        await using var harness = await RegistrationHarness.CreateAsync();

        foreach (var suffix in new[] { "one", "two" })
        {
            var response = await harness.Controller.RegisterStaff(new StaffRegisterRequest(
                FullName: $"Deneme Personel {suffix}",
                Email: $"staff-{suffix}@example.test",
                Password: null,
                PhoneNumber: null,
                CompanyCode: null,
                BranchCode: null,
                InviteCode: null,
                EmployeeCode: null,
                LoginMethod: LoginMethods.EmailCode,
                CompanyName: "Aynı Rastgele Firma"), default);

            var created = Assert.IsType<CreatedResult>(response.Result);
            var result = Assert.IsType<RegisterResponse>(created.Value);
            Assert.False(result.RequiresApproval);

            var user = await harness.Users.FindByEmailAsync($"staff-{suffix}@example.test");
            Assert.NotNull(user);
            Assert.True(user.IsActive);
            Assert.False(await harness.Users.HasPasswordAsync(user));
            Assert.Contains(Roles.Valet, await harness.Users.GetRolesAsync(user));
        }

        var companies = await harness.Db.Companies.AsNoTracking()
            .Where(x => x.Name == "Aynı Rastgele Firma")
            .ToListAsync();
        Assert.Equal(2, companies.Count);
        Assert.Equal(2, companies.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(2, await harness.Db.Branches.CountAsync(x => companies.Select(c => c.Id).Contains(x.CompanyId)));
        Assert.Equal(0, await harness.Db.RegistrationRequests.CountAsync());
    }

    [Fact]
    public async Task Staff_can_join_with_company_and_branch_codes_without_invite_but_cannot_skip_approval()
    {
        await using var h = await RegistrationHarness.CreateAsync();
        await h.Controller.RegisterOwner(new("Firma Sahibi", "boss@example.test", null, null, "Firma", LoginMethod: LoginMethods.EmailCode), default);
        var company = await h.Db.Companies.SingleAsync();
        var branch = await h.Db.Branches.SingleAsync();
        var response = await h.Controller.RegisterStaff(new("Yeni Personel", "staff@example.test", null, null, company.Code, branch.Code, null, LoginMethod: LoginMethods.EmailCode), default);
        var result = Assert.IsType<RegisterResponse>(Assert.IsType<CreatedResult>(response.Result).Value);
        Assert.True(result.RequiresApproval);
        var staff = await h.Users.FindByEmailAsync("staff@example.test");
        Assert.NotNull(staff);
        Assert.False(staff.IsActive);
        Assert.Equal(company.Id, staff.CompanyId);
        Assert.Single(await h.Db.RegistrationRequests.ToListAsync());
    }

    [Fact]
    public async Task Public_demo_company_cannot_receive_real_staff_registrations()
    {
        await using var h = await RegistrationHarness.CreateAsync();
        await DemoData.EnsureAsync(h.Db, h.Users);
        await Assert.ThrowsAsync<ApiException>(() => h.Controller.RegisterStaff(new("Yeni Personel", "staff@example.test", null, null, "VALEM-DEMO", "DEMO", null), default));
        Assert.Null(await h.Users.FindByEmailAsync("staff@example.test"));
    }

    private sealed class RegistrationHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        private RegistrationHarness(SqliteConnection connection, ServiceProvider provider)
        {
            _connection = connection;
            _provider = provider;
        }

        public ValeDbContext Db => _provider.GetRequiredService<ValeDbContext>();
        public UserManager<AppUser> Users => _provider.GetRequiredService<UserManager<AppUser>>();
        public TenantRegistrationController Controller => ActivatorUtilities.CreateInstance<TenantRegistrationController>(_provider);

        public static async Task<RegistrationHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.None));
            services.AddOptions();
            services.AddSingleton<IHttpContextAccessor, TestHttpContextAccessor>();
            services.AddDbContext<ValeDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<AppUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ValeDbContext>();
            services.AddSingleton<IOptions<EmailOptions>>(Options.Create(new EmailOptions()));
            services.AddSingleton<IOptions<FirebaseOptions>>(Options.Create(new FirebaseOptions()));
            services.AddScoped<CurrentUserContext>();
            services.AddScoped<TenantAccessService>();
            services.AddScoped<AuditService>();
            services.AddSingleton<FirebaseAppProvider>();
            services.AddScoped<FirebasePushSender>();
            services.AddScoped<IValeEmailSender, SmtpValeEmailSender>();

            var provider = services.BuildServiceProvider();
            var harness = new RegistrationHarness(connection, provider);
            await harness.Db.Database.EnsureCreatedAsync();
            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            foreach (var role in Roles.All)
                Assert.True((await roleManager.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
