using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using VALE.Api.Configuration;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var configuredConnectionString = builder.Configuration.GetConnectionString("ValeDatabase");
if (string.IsNullOrWhiteSpace(configuredConnectionString))
    throw new InvalidOperationException("ConnectionStrings:ValeDatabase tanımlı değil. Render Environment bölümünde Neon bağlantısını tanımlayın.");
var connectionString = NormalizePostgresConnectionString(configuredConnectionString);

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "JWT issuer gereklidir.")
    .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "JWT audience gereklidir.")
    .Validate(x => Encoding.UTF8.GetByteCount(x.Key) >= 32, "JWT anahtarı en az 32 bayt olmalıdır.")
    .Validate(x => x.ExpiryMinutes is >= 15 and <= 1440, "JWT süresi 15-1440 dakika arasında olmalıdır.")
    .ValidateOnStart();
builder.Services.AddOptions<SeedOptions>().Bind(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.AddOptions<BusinessRulesOptions>()
    .Bind(builder.Configuration.GetSection(BusinessRulesOptions.SectionName))
    .Validate(x => x.DefaultHourlyRate > 0, "Varsayılan saatlik ücret sıfırdan büyük olmalıdır.")
    .ValidateOnStart();
builder.Services.AddOptions<EmailOptions>().Bind(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddOptions<FirebaseOptions>().Bind(builder.Configuration.GetSection(FirebaseOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32) throw new InvalidOperationException("Jwt:Key en az 32 bayt olmalıdır.");

builder.Services.AddDbContext<ValeDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ValeDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "name",
        RoleClaimType = "role"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var subject = context.Principal?.FindFirst("sub")?.Value;
            var stamp = context.Principal?.FindFirst("security_stamp")?.Value;
            var tenantClaim = context.Principal?.FindFirst("company_id")?.Value;
            var branchClaim = context.Principal?.FindFirst("branch_id")?.Value;
            var tokenBranchId = Guid.TryParse(branchClaim, out var parsedBranchId) ? parsedBranchId : (Guid?)null;
            if (!Guid.TryParse(subject, out var userId) ||
                !Guid.TryParse(tenantClaim, out var companyId) ||
                string.IsNullOrWhiteSpace(stamp))
            {
                context.Fail("Oturum kimliği veya firma kapsamı eksik.");
                return;
            }

            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null ||
                !user.IsActive ||
                user.CompanyId != companyId ||
                !string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal) ||
                user.BranchId != tokenBranchId)
            {
                context.Fail("Oturum artık geçerli değil.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<ValeDbContext>();
            var companyActive = await db.Companies.AsNoTracking().AnyAsync(x => x.Id == companyId && x.IsActive, context.HttpContext.RequestAborted);
            var branchActive = !user.BranchId.HasValue || await db.Branches.AsNoTracking().AnyAsync(
                x => x.Id == user.BranchId.Value && x.CompanyId == companyId && x.IsActive,
                context.HttpContext.RequestAborted);
            if (!companyActive || !branchActive) context.Fail("Firma veya varsayılan şube artık aktif değil.");
        }
    };
});

var auth = builder.Services.AddAuthorizationBuilder();
auth.AddPolicy(Roles.StaffPolicy, p => p.RequireRole(Roles.All));
auth.AddPolicy(Roles.ManageUsersPolicy, p => p.RequireRole(Roles.ManageUsersRoles));
auth.AddPolicy(Roles.ManageBranchesPolicy, p => p.RequireRole(Roles.ManageBranchesRoles));
auth.AddPolicy(Roles.OperationWritePolicy, p => p.RequireRole(Roles.OperationWriteRoles));
auth.AddPolicy(Roles.FinancePolicy, p => p.RequireRole(Roles.FinanceRoles));
auth.AddPolicy(Roles.ReportsPolicy, p => p.RequireRole(Roles.ReportRoles));
auth.AddPolicy(Roles.AuditPolicy, p => p.RequireRole(Roles.AuditRoles));
auth.AddPolicy(Roles.RecordsEditPolicy, p => p.RequireRole(Roles.RecordsEditRoles));
auth.AddPolicy(Roles.DeleteRecordsPolicy, p => p.RequireRole(Roles.DeleteRecordsRoles));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => Fixed(context, 10, TimeSpan.FromMinutes(1)));
    options.AddPolicy("register", context => Fixed(context, 5, TimeSpan.FromMinutes(10)));
    options.AddPolicy("password-reset", context => Fixed(context, 5, TimeSpan.FromMinutes(15)));
    options.AddPolicy("email-code", context => Fixed(context, 5, TimeSpan.FromMinutes(10)));
    options.AddPolicy("2fa", context => Fixed(context, 10, TimeSpan.FromMinutes(5)));
    options.AddPolicy("diagnostic", context => Fixed(context, 3, TimeSpan.FromMinutes(5)));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<TenantAccessService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<PasswordResetCodeService>();
builder.Services.AddScoped<OneTimeCodeService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<IValeEmailSender, SmtpValeEmailSender>();
builder.Services.AddSingleton<FirebaseAppProvider>();
builder.Services.AddScoped<FirebasePushSender>();
builder.Services.AddSingleton<IFeeCalculator, FeeCalculator>();

var app = builder.Build();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Cache-Control"] = "no-store";
    await next();
});

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapHealthChecks("/health");
app.MapGet("/health/ready", async (ValeDbContext db, CancellationToken ct) =>
{
    try
    {
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready", database = "connected", utc = DateTimeOffset.UtcNow })
            : Results.Json(new { status = "not-ready", database = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.Json(new { status = "not-ready", database = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapGet("/health/email", async (IValeEmailSender email, CancellationToken ct) =>
{
    if (!email.IsConfigured)
        return Results.Json(new { status = "not-ready", smtp = false, stage = "not-configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);

    var probe = await email.ProbeAsync(ct);
    return probe.Success
        ? Results.Ok(new { status = "ready", smtp = true, stage = probe.Stage, utc = DateTimeOffset.UtcNow })
        : Results.Json(new { status = "not-ready", smtp = false, stage = probe.Stage }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous().RequireRateLimiting("diagnostic");

app.MapGet("/api/status", (IValeEmailSender email, FirebasePushSender push) => Results.Ok(new
{
    service = "VALE.Api",
    version = "3.1.2",
    status = "ok",
    capabilities = new
    {
        smtp = email.IsConfigured,
        fcm = push.IsConfigured
    },
    utc = DateTimeOffset.UtcNow
})).AllowAnonymous();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope()) await DatabaseSeeder.InitializeAsync(scope.ServiceProvider);
await app.RunAsync();

static RateLimitPartition<string> Fixed(HttpContext context, int permitLimit, TimeSpan window) =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0, AutoReplenishment = true });

static string NormalizePostgresConnectionString(string value)
{
    var trimmed = value.Trim();
    if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) return trimmed;
    var uri = new Uri(trimmed);
    var userInfo = uri.UserInfo.Split(':', 2);
    if (userInfo.Length != 2 || string.IsNullOrWhiteSpace(uri.Host)) throw new InvalidOperationException("Neon PostgreSQL bağlantı adresi geçersiz.");
    var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
    if (string.IsNullOrWhiteSpace(database)) throw new InvalidOperationException("Neon PostgreSQL bağlantı adresinde veritabanı adı bulunamadı.");
    return new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = Uri.UnescapeDataString(userInfo[1]),
        Database = database,
        SslMode = SslMode.Require,
        Timeout = 15,
        CommandTimeout = 30
    }.ConnectionString;
}

public partial class Program { }
