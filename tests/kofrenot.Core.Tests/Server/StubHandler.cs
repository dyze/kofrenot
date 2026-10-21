using System.Net;
using System.Text;

namespace Kofrenot.Core.Tests.Server;

/// <summary>A server answering with <paramref name="answer"/>, and the requests it got.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public static readonly Uri Server = new("https://kofre.example/");

    public List<(HttpMethod Method, string Path, string? Bearer, string? Body, string? Password)> Requests { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public HttpClient Client() => new(this) { BaseAddress = Server };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var password = request.Headers.TryGetValues(KofreClient.PasswordHeader, out var values) ? values.Single() : null;
        Requests.Add((request.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter, body, password));
        return answer(request);
    }
}
