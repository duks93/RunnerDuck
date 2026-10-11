using System.Net;
using System.Text;

namespace RunnerDuck.AIUsage.Tests;

internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly Lock gate = new();
    private readonly List<HttpRequestMessage> requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (gate)
            {
                return requests.ToList();
            }
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            requests.Add(request);
        }
        return Task.FromResult(respond(request));
    }
}

internal static class TestEnvironment
{
    public static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    public static AIUsageEnvironment Make(
        IReadOnlyDictionary<string, string>? files = null,
        StubHttpHandler? handler = null,
        Func<string, bool>? pathExists = null,
        DateTimeOffset? now = null
    )
    {
        var fileBodies = files ?? new Dictionary<string, string>();
        return new AIUsageEnvironment
        {
            HomeDirectory = "C:/Users/test",
            ReadFile = path => fileBodies
                .Where(file => path.Replace('\\', '/').EndsWith(file.Key, StringComparison.Ordinal))
                .Select(file => Encoding.UTF8.GetBytes(file.Value))
                .FirstOrDefault(),
            PathExists = path => pathExists?.Invoke(path.Replace('\\', '/')) ?? false,
            Http = new HttpClient(handler ?? new StubHttpHandler(_ => throw new HttpRequestException("offline"))),
            Now = () => now ?? Now,
        };
    }

    public static HttpResponseMessage Response(int statusCode, string body = "{}") =>
        new((HttpStatusCode)statusCode) { Content = new StringContent(body) };

    public static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
