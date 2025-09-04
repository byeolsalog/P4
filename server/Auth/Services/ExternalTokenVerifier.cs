using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Auth.Services;

public sealed class ExternalTokenVerifier
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _secret;
    private readonly int _retry;

    public ExternalTokenVerifier(IConfiguration cfg, HttpClient? injected = null)
    {
        _endpoint = cfg["Auth:VerifyEndpoint"] ?? throw new Exception("Auth:VerifyEndpoint missing");
        _secret   = cfg["Auth:BridgeSecret"]   ?? "";
        int timeout = cfg.GetValue("Auth:TimeoutMs", 3000);
        _retry     = cfg.GetValue("Auth:Retry", 1);
        _http = injected ?? new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
    }

    public sealed record VerifyResult(bool Ok, string? Sub, string? Email, string? Name, string? Reason);

    public async Task<VerifyResult> VerifyAsync(string provider, string idToken)
    {
        if (provider == "guest")
            return new(true, $"guest:{Guid.NewGuid():N}", null, "Guest", null);

        var body = new { provider, id_token = idToken };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(_secret))
            content.Headers.Add("X-Bridge-Secret", _secret); // 간단한 공유비밀 검증

        for (int i = 0; i <= _retry; i++)
        {
            try
            {
                using var res = await _http.PostAsync(_endpoint, content);
                if (!res.IsSuccessStatusCode)
                    return new(false, null, null, null, $"http { (int)res.StatusCode }");

                using var s = await res.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(s);
                var root = doc.RootElement;

                bool ok = root.GetProperty("ok").GetBoolean();
                if (!ok) return new(false, null, null, null, root.GetProperty("reason").GetString());

                string sub   = root.GetProperty("sub").GetString()!;
                string? mail = root.TryGetProperty("email", out var e) ? e.GetString() : null;
                string? name = root.TryGetProperty("name",  out var n) ? n.GetString() : null;
                return new(true, sub, mail, name, null);
            }
            catch (TaskCanceledException)
            {
                if (i == _retry) return new(false, null, null, null, "timeout");
            }
            catch (Exception ex)
            {
                if (i == _retry) return new(false, null, null, null, ex.Message);
            }
        }
        return new(false, null, null, null, "unknown");
    }
}