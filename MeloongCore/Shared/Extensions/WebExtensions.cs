namespace MeloongCore.Extensions;
public static class WebExtensions {

    // Source - https://stackoverflow.com/a/46026230, Posted by desautelsj, Retrieved 2026-06-14, License - CC BY-SA 4.0
    public static HttpRequestMessage Clone(this HttpRequestMessage request) {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) {
            Content = request.Content.Clone(),
            Version = request.Version
        };
        foreach (KeyValuePair<string, object> prop in request.Properties) clone.Properties.Add(prop);
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
    public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage request) {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) {
            Content = await request.Content.CloneAsync().NoCapture(),
            Version = request.Version
        };
        foreach (KeyValuePair<string, object> prop in request.Properties) clone.Properties.Add(prop);
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
    public static HttpContent? Clone(this HttpContent? content) {
        if (content == null) return null;
        var ms = new MemoryStream();
        content.CopyToAsync(ms).Wait();
        ms.Position = 0;
        var clone = new StreamContent(ms);
        foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers) clone.Headers.Add(header.Key, header.Value);
        return clone;
    }
    public static async Task<HttpContent?> CloneAsync(this HttpContent? content) {
        if (content == null) return null;
        var ms = new MemoryStream();
        await content.CopyToAsync(ms).NoCapture();
        ms.Position = 0;
        var clone = new StreamContent(ms);
        foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers) clone.Headers.Add(header.Key, header.Value);
        return clone;
    }

}
