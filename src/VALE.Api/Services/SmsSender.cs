using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace VALE.Api.Services;

public sealed class SmsOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "";
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string From { get; set; } = "";
}

public interface IValeSmsSender
{
    bool IsConfigured { get; }
    Task SendAsync(string phone, string code, CancellationToken ct);
}

public sealed class ValeSmsSender(IOptions<SmsOptions> options, IHttpClientFactory clients) : IValeSmsSender
{
    private readonly SmsOptions _options = options.Value;
    public bool IsConfigured => _options.Enabled && _options.Provider.Equals("Twilio", StringComparison.OrdinalIgnoreCase)
        && Regex.IsMatch(_options.AccountSid, "^AC[0-9a-fA-F]{32}$") && !string.IsNullOrWhiteSpace(_options.AuthToken) && !string.IsNullOrWhiteSpace(_options.From);
    public async Task SendAsync(string phone, string code, CancellationToken ct)
    {
        if (!IsConfigured) throw new ApiException(503, "SMS girişi hazır değil", "SMS ile giriş henüz etkin değil. E-posta veya parola ile giriş yapabilirsiniz.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{_options.AccountSid}/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.AccountSid}:{_options.AuthToken}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["To"] = phone, ["From"] = _options.From, ["Body"] = $"VALEM kodunuz: {code}. 10 dakika geçerli. Kimseyle paylaşmayın." });
        using var response = await clients.CreateClient("ValemSms").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new ApiException(503, "SMS gönderilemedi", "SMS şu anda gönderilemiyor. Diğer giriş yöntemlerinden birini kullanabilirsiniz.");
    }
}

public static class PhoneNumbers
{
    public static string Normalize(string input)
    {
        var value = input.Trim();
        if (value.Count(ch => ch == '+') > 1 || (value.Contains('+') && !value.StartsWith('+')))
            throw new ApiException(400, "Telefon numarası", "Ülke kodunu telefon numaranızın başına yazın.");
        if (value.Any(ch => !char.IsAsciiDigit(ch) && ch is not ('+' or ' ' or '-' or '(' or ')')))
            throw new ApiException(400, "Telefon numarası", "Geçerli telefon numaranızı ülke koduyla yazın.");
        var digits = string.Concat(value.Where(char.IsAsciiDigit));
        if (digits.StartsWith("00", StringComparison.Ordinal)) { digits = digits[2..]; value = "+" + digits; }
        if (!value.StartsWith('+'))
        {
            if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];
            if (digits.Length == 10 && digits.StartsWith('5')) digits = "90" + digits;
        }
        if (digits.Length is < 8 or > 15 || digits.StartsWith('0'))
            throw new ApiException(400, "Telefon numarası", "Telefon numaranızı kontrol edin. Örnek: +90 5xx xxx xx xx.");
        return "+" + digits;
    }
}
