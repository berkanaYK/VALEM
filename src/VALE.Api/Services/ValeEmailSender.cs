using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;

namespace VALE.Api.Services;

public interface IValeEmailSender
{
    bool IsConfigured { get; }
    Task SendPasswordResetCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken);
    Task SendLoginCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken);
}

public sealed class SmtpValeEmailSender(IOptions<EmailOptions> options, ILogger<SmtpValeEmailSender> logger) : IValeEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.Host) && _options.Port is > 0 and <= 65535 && !string.IsNullOrWhiteSpace(_options.FromEmail);

    public Task SendPasswordResetCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken) =>
        SendCodeAsync(email, fullName, code, "VALE şifre sıfırlama kodunuz", "şifre sıfırlama", 15, cancellationToken);

    public Task SendLoginCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken) =>
        SendCodeAsync(email, fullName, code, "VALE giriş kodunuz", "giriş", 10, cancellationToken);

    private async Task SendCodeAsync(string email, string fullName, string code, string subject, string purpose, int minutes, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta servisi hazır değil", "E-posta ile doğrulama şu anda kullanılamıyor.");

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = subject,
            Body = $"Merhaba {fullName},\n\nVALE {purpose} kodunuz: {code}\n\nKod {minutes} dakika geçerlidir. Bu işlemi siz başlatmadıysanız e-postayı yok sayın.\n",
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(email));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(_options.Username)) client.Credentials = new NetworkCredential(_options.Username, _options.Password);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            logger.LogError(ex, "VALE e-postası gönderilemedi. Amaç: {Purpose}", purpose);
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta gönderilemedi", "Doğrulama e-postası şu anda gönderilemiyor. Daha sonra tekrar deneyin.");
        }
    }
}
