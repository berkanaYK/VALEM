using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Controllers;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;
using Xunit;

namespace VALE.Api.Tests;

public sealed class EmailLoginTests
{
    [Fact]
    public async Task Reset_code_expires_and_resend_replaces_the_previous_code()
    {
        await using var h = await Harness.CreateAsync();
        var oldCode = await h.ResetCodes.CreateAsync(h.User);
        var newCode = await h.ResetCodes.CreateAsync(h.User);
        while (newCode == oldCode) newCode = await h.ResetCodes.CreateAsync(h.User);
        Assert.False(await h.ResetCodes.ValidateAndConsumeAsync(h.User, oldCode));
        Assert.True(await h.ResetCodes.ValidateAndConsumeAsync(h.User, newCode));
        await h.ResetCodes.CreateAsync(h.User);
        var stored = await h.Users.GetAuthenticationTokenAsync(h.User, "VALE", "PasswordResetCode");
        var expires = long.Parse(stored!.Split('.')[0], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(expires - DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 895, 900);
        await h.Users.SetAuthenticationTokenAsync(h.User, "VALE", "PasswordResetCode", $"{DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()}.00");
        Assert.False(await h.ResetCodes.ValidateAndConsumeAsync(h.User, newCode));
    }

    [Fact]
    public async Task Reset_code_guessing_is_account_scoped_and_locks_after_five_wrong_codes()
    {
        await using var h = await Harness.CreateAsync();
        var code = await h.ResetCodes.CreateAsync(h.User);
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<ApiException>(() => h.Controller.ResetPassword(new(h.User.Email!, "000000", "New!123")));
        var error = await Assert.ThrowsAsync<ApiException>(() => h.Controller.ResetPassword(new(h.User.Email!, code, "New!123")));
        Assert.Equal(423, error.StatusCode);
    }

    [Fact]
    public void Password_recovery_does_not_use_the_shared_ip_request_quota()
    {
        foreach (var name in new[] { nameof(AuthController.ForgotPassword), nameof(AuthController.ResetPassword) })
        {
            var method = typeof(AuthController).GetMethod(name)!;
            Assert.Single(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.DisableRateLimitingAttribute), true));
            Assert.Empty(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), true));
        }
    }

    [Fact]
    public async Task Rejected_password_does_not_consume_reset_code_and_success_blocks_replay()
    {
        await using var h = await Harness.CreateAsync();
        var code = await h.ResetCodes.CreateAsync(h.User);
        await Assert.ThrowsAsync<ApiException>(() => h.Controller.ResetPassword(new(h.User.Email!, code, "abcdef")));
        Assert.IsType<OkObjectResult>(await h.Controller.ResetPassword(new(h.User.Email!, code, "New!123")));
        Assert.True(await h.Users.CheckPasswordAsync(h.User, "New!123"));
        await Assert.ThrowsAsync<ApiException>(() => h.Controller.ResetPassword(new(h.User.Email!, code, "Next!123")));
    }

    [Fact]
    public async Task Wrong_reset_code_does_not_consume_valid_code()
    {
        await using var h = await Harness.CreateAsync();
        var code = await h.ResetCodes.CreateAsync(h.User);
        await Assert.ThrowsAsync<ApiException>(() => h.Controller.ResetPassword(new(h.User.Email!, "000000", "New!123")));
        Assert.IsType<OkObjectResult>(await h.Controller.ResetPassword(new(h.User.Email!, code, "New!123")));
    }

    [Fact]
    public async Task Password_login_accepts_username_or_email()
    {
        await using var h = await Harness.CreateAsync();

        var byUsername = await h.Controller.Login(new LoginRequest(h.User.UserName!, "Test!1"), default);
        var usernameLogin = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(byUsername.Result).Value);
        Assert.Equal(h.User.Email, usernameLogin.User.Email);

        var byEmail = await h.Controller.Login(new LoginRequest(h.User.Email!, "Test!1"), default);
        Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(byEmail.Result).Value);
    }

    [Fact]
    public async Task Password_login_requires_email_confirmation()
    {
        await using var h = await Harness.CreateAsync();
        h.User.EmailConfirmed = false;
        await h.Users.UpdateAsync(h.User);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            h.Controller.Login(new LoginRequest(h.User.UserName!, "Test!1"), default));

        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task Five_invalid_email_codes_lock_the_account_and_block_a_valid_code()
    {
        await using var h = await Harness.CreateAsync();
        var code = await h.Codes.CreateAsync(h.User, "email-login");
        for (var i = 0; i < 5; i++)
        {
            var error = await Assert.ThrowsAsync<ApiException>(() =>
                h.Controller.VerifyEmailLoginCode(new(h.User.Email!, "000000"), default));
            Assert.Equal(401, error.StatusCode);
        }
        Assert.True(await h.Users.IsLockedOutAsync(h.User));
        var locked = await Assert.ThrowsAsync<ApiException>(() =>
            h.Controller.VerifyEmailLoginCode(new(h.User.Email!, code), default));
        Assert.Equal(423, locked.StatusCode);
    }

    [Fact]
    public async Task Successful_email_login_resets_failures_and_consumes_the_code()
    {
        await using var h = await Harness.CreateAsync();
        await h.Users.AccessFailedAsync(h.User);
        await h.Users.AccessFailedAsync(h.User);
        var code = await h.Codes.CreateAsync(h.User, "email-login");
        var response = await h.Controller.VerifyEmailLoginCode(new(h.User.Email!, code), default);
        var login = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal(0, await h.Users.GetAccessFailedCountAsync(h.User));
        var replay = await Assert.ThrowsAsync<ApiException>(() =>
            h.Controller.VerifyEmailLoginCode(new(h.User.Email!, code), default));
        Assert.Equal(401, replay.StatusCode);
    }

    [Fact]
    public async Task Invalid_authenticator_code_counts_as_a_failed_email_login()
    {
        await using var h = await Harness.CreateAsync();
        Assert.True((await h.Users.SetTwoFactorEnabledAsync(h.User, true)).Succeeded);
        // No authenticator key is installed: every supplied TOTP must fail.
        var code = await h.Codes.CreateAsync(h.User, "email-login");
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            h.Controller.VerifyEmailLoginCode(new(h.User.Email!, code, "123456"), default));
        Assert.Equal(401, error.StatusCode);
        Assert.Equal(1, await h.Users.GetAccessFailedCountAsync(h.User));
    }

    private sealed class Harness(SqliteConnection connection, ServiceProvider provider, AppUser user) : IAsyncDisposable
    {
        public AppUser User { get; } = user;
        public UserManager<AppUser> Users => provider.GetRequiredService<UserManager<AppUser>>();
        public OneTimeCodeService Codes => provider.GetRequiredService<OneTimeCodeService>();
        public PasswordResetCodeService ResetCodes => provider.GetRequiredService<PasswordResetCodeService>();
        public AuthController Controller => ActivatorUtilities.CreateInstance<AuthController>(provider);

        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddSingleton<IHttpContextAccessor, TestHttpContextAccessor>();
            services.AddDbContext<ValeDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<AppUser>(options => options.Lockout.MaxFailedAccessAttempts = 5)
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ValeDbContext>()
                .AddDefaultTokenProviders();
            services.AddSingleton<IOptions<JwtOptions>>(Options.Create(new JwtOptions { Key = new string('x', 64) }));
            services.AddSingleton<IOptions<DeviceSessionOptions>>(Options.Create(new DeviceSessionOptions()));
            services.AddSingleton<IOptions<EmailOptions>>(Options.Create(new EmailOptions()));
            services.AddScoped<IValeEmailSender, SmtpValeEmailSender>();
            services.AddScoped<CurrentUserContext>();
            services.AddScoped<AuditService>();
            services.AddScoped<TokenService>();
            services.AddScoped<DeviceSessionService>();
            services.AddScoped<PasswordResetCodeService>();
            services.AddScoped<OneTimeCodeService>();
            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<ValeDbContext>();
            await db.Database.EnsureCreatedAsync();
            var company = new Company { Name = "Login Test", Code = "LOGIN-TEST" };
            var branch = new Branch { Company = company, CompanyId = company.Id, Name = "Merkez", Code = "MRKZ", City = "Antalya" };
            db.AddRange(company, branch);
            await db.SaveChangesAsync();
            var user = new AppUser
            {
                UserName = "login_user",
                Email = "login@example.test",
                FullName = "Login Test",
                Company = company,
                CompanyId = company.Id,
                Branch = branch,
                BranchId = branch.Id,
                IsActive = true,
                EmailConfirmed = true
            };
            Assert.True((await provider.GetRequiredService<UserManager<AppUser>>().CreateAsync(user, "Test!1")).Succeeded);
            return new Harness(connection, provider, user);
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}

// Controller tests run without ASP.NET middleware; provide an anonymous request
// without relying on HttpContextAccessor's AsyncLocal flowing out of async setup.
internal sealed class TestHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
}
