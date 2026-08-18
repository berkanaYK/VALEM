using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
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
    private bool UseBrevo => string.Equals(_options.Transport, "Brevo", StringComparison.OrdinalIgnoreCase);

    public bool IsConfigured
    {
        get
        {
            if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.FromEmail)) return false;
            if (UseBrevo)
                return !string.IsNullOrWhiteSpace(_options.BrevoApiKey)
                    && Uri.TryCreate(_options.BrevoBaseUrl, UriKind.Absolute, out _);

            var credentialPairIsValid = string.IsNullOrWhiteSpace(_options.Username)
                ? string.IsNullOrWhiteSpace(_options.Password)
                : !string.IsNullOrWhiteSpace(_options.Password);
            return !string.IsNullOrWhiteSpace(_options.Host)
                && _options.Port is > 0 and <= 65535
                && credentialPairIsValid;
        }
    }

    public Task<SmtpProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        UseBrevo ? ProbeBrevoAsync(cancellationToken) : ProbeSmtpAsync(cancellationToken);

    public Task SendPasswordResetCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken) =>
        SendCodeAsync(email, fullName, code, "VALE şifre sıfırlama kodunuz", "şifre sıfırlama", 15, cancellationToken);

    public Task SendLoginCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken) =>
        SendCodeAsync(email, fullName, code, "VALE giriş kodunuz", "giriş", 10, cancellationToken);

    private async Task<SmtpProbeResult> ProbeBrevoAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured) return new SmtpProbeResult(false, "brevo-not-configured");
        try
        {
            using var client = CreateBrevoClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "senders");
            request.Headers.TryAddWithoutValidation("api-key", _options.BrevoApiKey.Trim());
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new SmtpProbeResult(false, $"brevo-api-{(int)response.StatusCode}");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!json.RootElement.TryGetProperty("senders", out var senders) || senders.ValueKind != JsonValueKind.Array)
                return new SmtpProbeResult(false, "brevo-senders-invalid");

            foreach (var sender in senders.EnumerateArray())
            {
                var email = sender.TryGetProperty("email", out var emailNode) ? emailNode.GetString() : null;
                var active = sender.TryGetProperty("active", out var activeNode) && activeNode.ValueKind == JsonValueKind.True;
                if (active && string.Equals(email, _options.FromEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                    return new SmtpProbeResult(true, "brevo-api-sender-ready");
            }

            return new SmtpProbeResult(false, "brevo-sender-not-active");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "VALE Brevo HTTPS sağlık kontrolü başarısız. API key veya içerik loglanmadı.");
            return new SmtpProbeResult(false, "brevo-connection-failed");
        }
    }

    private async Task<SmtpProbeResult> ProbeSmtpAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured) return new SmtpProbeResult(false, "smtp-not-configured");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            var ct = timeout.Token;

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(_options.Host, _options.Port, ct);
            Stream transport = tcp.GetStream();

            var session = CreateSession(transport);
            var banner = await ReadResponseCodeAsync(session.Reader, ct);
            if (banner != 220) return new SmtpProbeResult(false, $"banner-{banner}");

            var ehlo = await SendCommandAsync(session, "EHLO vale-health", ct);
            if (ehlo != 250) return new SmtpProbeResult(false, $"ehlo-{ehlo}");

            if (_options.EnableSsl)
            {
                var startTls = await SendCommandAsync(session, "STARTTLS", ct);
                if (startTls != 220) return new SmtpProbeResult(false, $"starttls-{startTls}");

                session.Dispose();
                var ssl = new SslStream(transport, leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = _options.Host }, ct);
                transport = ssl;
                session = CreateSession(transport);

                ehlo = await SendCommandAsync(session, "EHLO vale-health", ct);
                if (ehlo != 250) return new SmtpProbeResult(false, $"tls-ehlo-{ehlo}");
            }

            var stage = _options.EnableSsl ? "tls" : "connected";
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                var auth = await SendCommandAsync(session, "AUTH LOGIN", ct);
                if (auth != 334) return new SmtpProbeResult(false, $"auth-start-{auth}");
                auth = await SendCommandAsync(session, Convert.ToBase64String(Encoding.UTF8.GetBytes(_options.Username)), ct);
                if (auth != 334) return new SmtpProbeResult(false, $"auth-user-{auth}");
                auth = await SendCommandAsync(session, Convert.ToBase64String(Encoding.UTF8.GetBytes(_options.Password)), ct);
                if (auth != 235) return new SmtpProbeResult(false, $"auth-password-{auth}");
                stage = "authenticated";
            }

            var mailFrom = await SendCommandAsync(session, $"MAIL FROM:<{_options.FromEmail}>", ct);
            if (mailFrom != 250) return new SmtpProbeResult(false, $"mail-from-{mailFrom}");
            var rcptTo = await SendCommandAsync(session, $"RCPT TO:<{_options.FromEmail}>", ct);
            if (rcptTo is not (250 or 251)) return new SmtpProbeResult(false, $"rcpt-to-{rcptTo}");
            _ = await SendCommandAsync(session, "RSET", ct);
            _ = await SendCommandAsync(session, "QUIT", ct);
            session.Dispose();
            return new SmtpProbeResult(true, stage == "authenticated" ? "authenticated-envelope" : "envelope-ready");
        }
        catch (OperationCanceledException)
        {
            return new SmtpProbeResult(false, "smtp-network-timeout");
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or InvalidOperationException)
        {
            logger.LogWarning(ex, "VALE SMTP sağlık kontrolü başarısız. Credential ayrıntısı loglanmadı.");
            return new SmtpProbeResult(false, "smtp-connection-failed");
        }
    }

    private async Task SendCodeAsync(string email, string fullName, string code, string subject, string purpose, int minutes, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta servisi hazır değil", "E-posta ile doğrulama şu anda kullanılamıyor.");

        var body = $"Merhaba {fullName},\n\nVALE {purpose} kodunuz: {code}\n\nKod {minutes} dakika geçerlidir. Bu işlemi siz başlatmadıysanız e-postayı yok sayın.\n";
        if (UseBrevo)
        {
            await SendBrevoAsync(email, fullName, subject, body, purpose, cancellationToken);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = subject,
            Body = body,
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
            logger.LogError(ex, "VALE SMTP e-postası gönderilemedi. Amaç: {Purpose}", purpose);
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta gönderilemedi", "Doğrulama e-postası şu anda gönderilemiyor. Daha sonra tekrar deneyin.");
        }
    }

    private async Task SendBrevoAsync(string email, string fullName, string subject, string body, string purpose, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateBrevoClient();
            var payload = JsonSerializer.Serialize(new
            {
                sender = new { email = _options.FromEmail.Trim(), name = _options.FromName },
                to = new[] { new { email = email.Trim(), name = fullName } },
                subject,
                textContent = body
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("api-key", _options.BrevoApiKey.Trim());
            using var response = await client.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode != StatusCodes.Status201Created)
            {
                logger.LogWarning("Brevo e-posta isteği reddedildi. Amaç: {Purpose}; HTTP: {Status}", purpose, (int)response.StatusCode);
                throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta gönderilemedi", "E-posta sağlayıcısı gönderimi kabul etmedi. Daha sonra tekrar deneyin.");
            }
        }
        catch (ApiException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "VALE Brevo HTTPS e-postası gönderilemedi. Amaç: {Purpose}", purpose);
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "E-posta gönderilemedi", "E-posta sağlayıcısına ulaşılamıyor. Daha sonra tekrar deneyin.");
        }
    }

    private HttpClient CreateBrevoClient()
    {
        var baseUrl = _options.BrevoBaseUrl.Trim().TrimEnd('/') + "/";
        return new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(15) };
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
