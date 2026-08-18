namespace VALE.Api.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }
    public string Transport { get; set; } = "Smtp";
    public string BrevoApiKey { get; set; } = string.Empty;
    public string BrevoBaseUrl { get; set; } = "https://api.brevo.com/v3";
    public string PublicBaseUrl { get; set; } = "https://vale-api-5fvb.onrender.com";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "VALE";
    public bool EnableSsl { get; set; } = true;
}
