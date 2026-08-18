using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;

namespace VALE.Api.Services;

public sealed record SmtpProbeResult(bool Success, string Stage);

public interface IValeEmailSender
{
    bool IsConfigured { get; }
    Task<SmtpProbeResult> ProbeAsync(CancellationToken cancellationToken);
    Task SendPasswordResetCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken);
    Task SendLoginCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken);
}

public sealed class SmtpValeEmailSender(IOptions<EmailOptions> options, ILogger<SmtpValeEmailSender> logger) : IValeEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured
    {
        get
        {
            var credentialPairIsValid = string.IsNullOrWhiteSpace(_options.Username)
                ? string.IsNullOrWhiteSpace(_options.Password)
                : !string.IsNullOrWhiteSpace(_options.Password);
            return _options.Enabled
                && !string.IsNullOrWhiteSpace(_options.Host)
                && _options.Port is > 0 and <= 65535
                && !string.IsNullOrWhiteSpace(_options.FromEmail)
                && credentialPairIsValid;
        }
    }

    public async Task<SmtpProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured) return new SmtpProbeResult(false, "not-configured");

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(_options.Host, _options.Port, cancellationToken);
            Stream transport = tcp.GetStream();

            var session = CreateSession(transport);
            var banner = await ReadResponseCodeAsync(session.Reader, cancellationToken);
            if (banner != 220) return new SmtpProbeResult(false, $"banner-{banner}");

            var ehlo = await SendCommandAsync(session, "EHLO vale-health", cancellationToken);
            if (ehlo != 250) return new SmtpProbeResult(false, $"ehlo-{ehlo}");

            if (_options.EnableSsl)
            {
                var startTls = await SendCommandAsync(session, "STARTTLS", cancellationToken);
                if (startTls != 220) return new SmtpProbeResult(false, $"starttls-{startTls}");

                session.Dispose();
                var ssl = new SslStream(transport, leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = _options.Host
                }, cancellationToken);
                transport = ssl;
                session = CreateSession(transport);

                ehlo = await SendCommandAsync(session, "EHLO vale-health", cancellationToken);
                if (ehlo != 250) return new SmtpProbeResult(false, $"tls-ehlo-{ehlo}");
            }

            var stage = _options.EnableSsl ? "tls" : "connected";
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                var auth = await SendCommandAsync(session, "AUTH LOGIN", cancellationToken);
                if (auth != 334) return new SmtpProbeResult(false, $"auth-start-{auth}");

                auth = await SendCommandAsync(session, Convert.ToBase64String(Encoding.UTF8.GetBytes(_options.Username)), cancellationToken);
                if (auth != 334) return new SmtpProbeResult(false, $"auth-user-{auth}");

                auth = await SendCommandAsync(session, Convert.ToBase64String(Encoding.UTF8.GetBytes(_options.Password)), cancellationToken);
                if (auth != 235) return new SmtpProbeResult(false, $"auth-password-{auth}");
                stage = "authenticated";
            }

            // Verify the configured sender/recipient envelope without transmitting message content.
            var mailFrom = await SendCommandAsync(session, $"MAIL FROM:<{_options.FromEmail}>", cancellationToken);
            if (mailFrom != 250) return new SmtpProbeResult(false, $"mail-from-{mailFrom}");
            var rcptTo = await SendCommandAsync(session, $"RCPT TO:<{_options.FromEmail}>", cancellationToken);
            if (rcptTo is not (250 or 251)) return new SmtpProbeResult(false, $"rcpt-to-{rcptTo}");
            _ = await SendCommandAsync(session, "RSET", cancellationToken);
            _ = await SendCommandAsync(session, "QUIT", cancellationToken);
            session.Dispose();

            return new SmtpProbeResult(true, stage == "authenticated" ? "authenticated-envelope" : "envelope-ready");
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or InvalidOperationException)
        {
            logger.LogWarning(ex, "VALE SMTP sağlık kontrolü başarısız. Credential ayrıntısı loglanmadı.");
            return new SmtpProbeResult(false, "connection-failed");
        }
    }

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

    private static SmtpSession CreateSession(Stream stream) => new(
        new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true),
        new StreamWriter(stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true });

    private static async Task<int> SendCommandAsync(SmtpSession session, string command, CancellationToken cancellationToken)
    {
        await session.Writer.WriteLineAsync(command.AsMemory(), cancellationToken);
        return await ReadResponseCodeAsync(session.Reader, cancellationToken);
    }

    private static async Task<int> ReadResponseCodeAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null || line.Length < 3 || !int.TryParse(line.AsSpan(0, 3), out var parsed))
                throw new IOException("SMTP sunucusundan geçerli yanıt alınamadı.");
            if (line.Length < 4 || line[3] != '-') return parsed;
        }
    }

    private sealed class SmtpSession(StreamReader reader, StreamWriter writer) : IDisposable
    {
        public StreamReader Reader { get; } = reader;
        public StreamWriter Writer { get; } = writer;
        public void Dispose()
        {
            Writer.Dispose();
            Reader.Dispose();
        }
    }
}
