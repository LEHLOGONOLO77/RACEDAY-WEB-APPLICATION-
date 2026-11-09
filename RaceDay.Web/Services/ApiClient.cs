using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceDay.Web.Services;

public class ApiResult
{
    public HttpStatusCode Status { get; init; }
    public string? Error { get; init; }
    public bool Ok => (int)Status is >= 200 and < 300;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}

/// <summary>
/// Talks to the RaceDay API on behalf of the signed-in user. The API's session cookie is kept in the
/// MVC session and replayed on every call, so the API stays the single source of truth for who may do what.
/// </summary>
public class ApiClient
{
    private const string ApiCookieName = "RaceDay.Session";
    private const string Unreachable = "We couldn't reach the RaceDay service. Please try again shortly.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;

    public ApiClient(HttpClient http, IHttpContextAccessor context)
    {
        _http = http;
        _context = context;
    }

    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url);
    public Task<ApiResult<T>> PostAsync<T>(string url, object body) => SendAsync<T>(HttpMethod.Post, url, body);
    public Task<ApiResult<T>> PutAsync<T>(string url, object body) => SendAsync<T>(HttpMethod.Put, url, body);
    public Task<ApiResult> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);
    public Task<ApiResult> PutAsync(string url, object body) => SendAsync(HttpMethod.Put, url, body);
    public Task<ApiResult> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url);

    /// <summary>Sends an image to the API (which stores it in Azure Blob Storage).</summary>
    public async Task<ApiResult<T>> UploadAsync<T>(string url, IFormFile file)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var part = new StreamContent(stream);
        part.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        form.Add(part, "file", file.FileName);
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        return await SendCoreAsync<T>(request);
    }

    private Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null) =>
        SendCoreAsync<T>(Build(method, url, body));

    private async Task<ApiResult> SendAsync(HttpMethod method, string url, object? body = null) =>
        await SendCoreAsync<object>(Build(method, url, body), parseBody: false);

    private static HttpRequestMessage Build(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        return request;
    }

    private async Task<ApiResult<T>> SendCoreAsync<T>(HttpRequestMessage request, bool parseBody = true)
    {
        var session = _context.HttpContext?.Session;
        var cookie = session?.GetString(SessionExtensions.ApiCookieKey);
        if (!string.IsNullOrEmpty(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", $"{ApiCookieName}={cookie}");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ApiResult<T> { Status = HttpStatusCode.ServiceUnavailable, Error = Unreachable };
        }

        using (response)
        {
            StoreRefreshedCookie(response, session);

            if (response.IsSuccessStatusCode)
            {
                T? data = default;
                if (parseBody && response.StatusCode != HttpStatusCode.NoContent)
                {
                    try { data = await response.Content.ReadFromJsonAsync<T>(Json); }
                    catch (JsonException) { return new ApiResult<T> { Status = HttpStatusCode.BadGateway, Error = Unreachable }; }
                }
                return new ApiResult<T> { Status = response.StatusCode, Data = data };
            }

            // A 401 for someone who was signed in means the API session ended: AppController sends them to log in again.
            if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(cookie))
                _context.HttpContext!.Items["SessionExpired"] = true;

            return new ApiResult<T>
            {
                Status = response.StatusCode,
                Error = await FriendlyErrorAsync(response)
            };
        }
    }

    /// <summary>The API refreshes its sliding cookie; keep the newest value.</summary>
    private static void StoreRefreshedCookie(HttpResponseMessage response, ISession? session)
    {
        if (session is null || !response.Headers.TryGetValues("Set-Cookie", out var values)) return;
        foreach (var header in values)
        {
            if (!header.StartsWith(ApiCookieName + "=", StringComparison.Ordinal)) continue;
            var value = header[(ApiCookieName.Length + 1)..].Split(';')[0];
            if (!string.IsNullOrEmpty(value))
                session.SetString(SessionExtensions.ApiCookieKey, value);
        }
    }

    /// <summary>Turns an API error into a short sentence safe to show to a user. Never raw stack traces or HTML.</summary>
    private static async Task<string> FriendlyErrorAsync(HttpResponseMessage response)
    {
        string text;
        try { text = (await response.Content.ReadAsStringAsync()).Trim(); }
        catch { text = ""; }

        var fromApi = ExtractMessage(text);
        if (fromApi is not null) return fromApi;

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Some of the details you entered are not valid.",
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.NotFound => "We couldn't find what you were looking for.",
            HttpStatusCode.Conflict => "That conflicts with existing information.",
            _ => "Something went wrong on our side. Please try again."
        };
    }

    private static string? ExtractMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (text.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = errors.EnumerateObject()
                        .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array ? p.Value.EnumerateArray() : Enumerable.Empty<JsonElement>())
                        .Select(m => m.GetString())
                        .Where(m => !string.IsNullOrWhiteSpace(m) && !m!.Contains("JSON") && !m.Contains("$."))
                        .Distinct().ToList();
                    return messages.Count > 0 ? string.Join(" ", messages) : "Some of the details you entered are not valid.";
                }
                if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    return Clean(detail.GetString());
            }
            catch (JsonException) { }
            return null;
        }

        return Clean(text.Trim('"'));
    }

    private static string? Clean(string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 200) return null;
        if (message.Contains('<') || message.Contains("Exception") || message.Contains(" at ")) return null;
        return message;
    }
}
