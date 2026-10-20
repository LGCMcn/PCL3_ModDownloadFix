namespace MeloongCore;

/// <summary>
/// 当 HTTP 请求的状态码不指示成功时引发的异常。
/// </summary>
public class HttpRequestCodeException(string message, HttpStatusCode statusCode, string? response) : HttpRequestException(message) {
    /// <summary>
    /// HTTP 请求的状态码。
    /// </summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
    /// <summary>
    /// 远程服务器给予的回复。
    /// </summary>
    public string? Response { get; } = response;
}
