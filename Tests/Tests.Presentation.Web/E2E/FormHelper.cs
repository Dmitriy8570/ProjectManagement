using System.Text.RegularExpressions;

namespace Tests.Presentation.Web.E2E;

/// <summary>
/// Helpers for posting to anti-forgery–protected MVC forms. Each form GET
/// returns a hidden <c>__RequestVerificationToken</c> and a paired
/// <c>.AspNetCore.Antiforgery.*</c> cookie; the cookie is persisted by the
/// HttpClient's default cookie container, so callers only need to thread the
/// token value through to the next POST.
/// </summary>
internal static class FormHelper
{
    private static readonly Regex TokenRegex = new(
        @"name=""__RequestVerificationToken""[^>]*value=""(?<token>[^""]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// GETs <paramref name="pageUrl"/> and returns the antiforgery token rendered
    /// into the page's form. Also sets the matching antiforgery cookie on the
    /// client's cookie container as a side effect.
    /// </summary>
    public static async Task<string> FetchTokenAsync(
        this HttpClient client, string pageUrl, CancellationToken ct)
    {
        var resp = await client.GetAsync(pageUrl, ct);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync(ct);

        var match = TokenRegex.Match(html);
        if (!match.Success)
            throw new InvalidOperationException(
                $"Antiforgery token not found in response from {pageUrl}.");

        return match.Groups["token"].Value;
    }

    /// <summary>
    /// POSTs a urlencoded form, automatically adding the antiforgery token field.
    /// Lists are sent as repeated keys (the binding convention MVC expects).
    /// </summary>
    public static Task<HttpResponseMessage> PostFormAsync(
        this HttpClient client,
        string url,
        IEnumerable<KeyValuePair<string, string?>> fields,
        string token,
        CancellationToken ct)
    {
        var payload = fields
            .Append(new KeyValuePair<string, string?>("__RequestVerificationToken", token))
            .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value ?? string.Empty));

        return client.PostAsync(url, new FormUrlEncodedContent(payload), ct);
    }
}
