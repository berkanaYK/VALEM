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
{
    throw new InvalidOperationException(
        "ConnectionStrings:ValeDatabase tanımlı değil. Render Environment bölümünde Neon bağlantısını tanımlayın.");
}

var connectionString = NormalizePostgresConnectionString(configuredConnectionString);

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "JWT issuer gereklidir.")
    .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "JWT audience gereklidir.")
    .Validate(x => Encoding.UTF8.GetByteCount(x.Key) >= 32, "JWT anahtarı en az 32 bayt olmalıdır.")
    .Validate(x => x.ExpiryMinutes is >= 15 and <= 1440, "JWT süresi 15-1440 dakika arasında olmalıdır.")
    .ValidateOnStart();
builder.Services.AddOptions<SeedOptions>()
    .Bind(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.AddOptions<BusinessRulesOptions>()
    .Bind(builder.Configuration.GetSection(BusinessRulesOptions.SectionName))
    .Validate(x => x.DefaultHourlyRate > 0, "Varsayılan saatlik ücret sıfırdan büyük olmalıdır.")
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key en az 32 bayt olmalıdır. scripts/configure-api.ps1 ile güvenli yapılandırmayı tamamlayın.");
}

builder.Services.AddDbContext<ValeDbContext>(options => options.UseNpgsql(connectionString));
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ValeDbContext>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Roles.StaffPolicy, policy => policy.RequireRole(Roles.All));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddSingleton<IFeeCalculator, FeeCalculator>();

var app = builder.Build();

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseSeeder.InitializeAsync(scope.ServiceProvider);
}

await app.RunAsync();

static string NormalizePostgresConnectionString(string value)
{
    var trimmed = value.Trim();
    if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return trimmed;
    }

    var uri = new Uri(trimmed);
    var userInfo = uri.UserInfo.Split(':', 2);
    if (userInfo.Length != 2 || string.IsNullOrWhiteSpace(uri.Host))
    {
        throw new InvalidOperationException("Neon PostgreSQL bağlantı adresi geçersiz.");
    }

    var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
    if (string.IsNullOrWhiteSpace(database))
    {
        throw new InvalidOperationException("Neon PostgreSQL bağlantı adresinde veritabanı adı bulunamadı.");
    }

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

public partial class Program
{
}
