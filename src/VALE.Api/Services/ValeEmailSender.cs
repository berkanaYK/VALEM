using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;

namespace VALE.Api.Services;

public interface IValeEmailSender
{
    bool IsConfigured { get; }
    Task SendPasswordResetCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken);
}

public sealed class SmtpValeEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpValeEmailSender> logger) : IValeEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured =>
        _options.Enabled &&
        !string.IsNullOrWhiteSpace(_options.Host) &&
        _options.Port is > 0 and <= 65535 &&
        !string.IsNullOrWhiteSpace(_options.FromEmail);

    public async Task SendPasswordResetCodeAsync(
        string email,
        string fullName,
        string code,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new ApiException(
                StatusCodes.Status503ServiceUnavailable,
                "E-posta servisi hazır değil",
                "Şifre sıfırlama e-postası için sunucu SMTP ayarları henüz yapılandırılmamış.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "VALE şifre sıfırlama kodunuz",
            Body = $"Merhaba {fullName},\n\nVALE şifre sıfırlama kodunuz: {code}\n\nKod 15 dakika geçerlidir. Bu işlemi siz başlatmadıysanız e-postayı yok sayın.\n",
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(email));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            logger.LogError(ex, "VALE şifre sıfırlama e-postası gönderilemedi.");
            throw new ApiException(
                StatusCodes.Status503ServiceUnavailable,
                "E-posta gönderilemedi",
                "Şifre sıfırlama e-postası şu anda gönderilemiyor. Daha sonra tekrar deneyin.");
        }
    }
}
