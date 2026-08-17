using System.Text.Json;

namespace VALE.Client.Services;

public sealed class ClientSettings
{
    public string ApiBaseUrl { get; init; } = "https://localhost:7247/";

    public static ClientSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            return new ClientSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new ClientSettings();
        }
        catch (JsonException)
        {
            return new ClientSettings();
        }
    }

    public Uri GetApiBaseUri()
    {
        var value = ApiBaseUrl.EndsWith('/') ? ApiBaseUrl : ApiBaseUrl + "/";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("appsettings.json içindeki ApiBaseUrl geçerli bir adres değil.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("VALE API adresi HTTPS kullanmalıdır.");
        }

        return uri;
    }
}
