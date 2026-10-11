using System.Net;
using RunnerDuck.Core;

namespace RunnerDuck.Core.Tests;

public sealed class AppUpdateServiceTests
{
    private const string Commit = "3d9c719e0969785e4b0e8a57f0911b8c63a501dd";

    private const string NewerCommit = "25568fdb1e940c160ea8850084df218c1f4c81cb";
    private const string CurrentFeed = "{\"schemaVersion\":1,\"commit\":\"" + Commit + "\",\"ancestors\":[]}";
    private const string NewerFeed = "{\"schemaVersion\":1,\"commit\":\"" + NewerCommit + "\",\"ancestors\":[\"" + Commit + "\"]}";

    [Theory]
    [InlineData(CurrentFeed, AppUpdateStatus.UpToDate)]
    [InlineData(NewerFeed, AppUpdateStatus.Available)]
    [InlineData("{\"schemaVersion\":1,\"commit\":\"" + NewerCommit + "\",\"ancestors\":[]}", AppUpdateStatus.UnpublishedBuild)]
    public async Task ComparesRevisionAgainstPublishedHistory(string body, AppUpdateStatus expected)
    {
        using var client = Client(body);
        Assert.Equal(expected, (await new AppUpdateService(client, Commit).CheckAsync()).Status);
    }

    [Theory]
    [InlineData(CurrentFeed)]
    [InlineData("{\"schemaVersion\":1,\"commit\":\"" + NewerCommit + "\",\"ancestors\":[]}")]
    public async Task OnlyAvailableUpdateReportsLatestCommit(string body)
    {
        using var client = Client(body);
        Assert.Null((await new AppUpdateService(client, Commit).CheckAsync()).LatestCommit);
    }

    [Theory]
    [InlineData(CurrentFeed)]
    [InlineData(NewerFeed)]
    public async Task UppercaseFeedRevisionsAreRejected(string body)
    {
        using var client = Client(body.Replace(Commit, Commit.ToUpperInvariant()));
        Assert.Equal(AppUpdateStatus.Failed, (await new AppUpdateService(client, Commit).CheckAsync()).Status);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task HttpFailureDoesNotReportCurrent(int status)
    {
        using var client = Client(CurrentFeed, (HttpStatusCode)status);
        Assert.Equal(AppUpdateStatus.Failed, (await new AppUpdateService(client, Commit).CheckAsync()).Status);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("invalid")]
    [InlineData("{\"schemaVersion\":2,\"commit\":\"" + Commit + "\",\"ancestors\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"commit\":\"bad\",\"ancestors\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"commit\":\"" + Commit + "\",\"ancestors\":[\"bad\"]}")]
    [InlineData("{\"schemaVersion\":1,\"commit\":\"" + Commit + "\",\"ancestors\":[\"" + Commit + "\"]}")]
    public async Task InvalidResponseReportsFailure(string body)
    {
        using var client = Client(body);
        Assert.Equal(AppUpdateStatus.Failed, (await new AppUpdateService(client, Commit).CheckAsync()).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("main")]
    [InlineData("../main")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task UnknownBuildDoesNotRequest(string? commit)
    {
        using var client = new HttpClient(new Handler(_ => throw new InvalidOperationException("Must not request")));
        var service = new AppUpdateService(client, commit);
        Assert.Equal(AppUpdateStatus.UnknownBuild, (await service.CheckAsync()).Status);
    }

    [Fact]
    public async Task NetworkFailureReportsFailure()
    {
        using var client = new HttpClient(new Handler(_ => throw new HttpRequestException("Offline")));
        Assert.Equal(AppUpdateStatus.Failed, (await new AppUpdateService(client, Commit).CheckAsync()).Status);
    }

    [Fact]
    public async Task CancellationReportsFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new HttpClient(new Handler(_ => throw new OperationCanceledException()));
        Assert.Equal(AppUpdateStatus.Failed, (await new AppUpdateService(client, Commit).CheckAsync(cancellation.Token)).Status);
    }

    [Fact]
    public async Task PublicFeedRequestSendsNoRevisionOrCredentials()
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal("https://github.com/duks93/RunnerDuck/releases/latest/download/latest.json", request.RequestUri!.AbsoluteUri);
            Assert.Equal("RunnerDuck", request.Headers.UserAgent.ToString());
            Assert.Null(request.Headers.Authorization);
            return new(HttpStatusCode.OK) { Content = new StringContent(NewerFeed) };
        }));
        var service = new AppUpdateService(client, Commit.ToUpperInvariant());
        Assert.Equal(new AppUpdateCheck(AppUpdateStatus.Available, NewerCommit), await service.CheckAsync());
        Assert.Equal("https://github.com/duks93/RunnerDuck/releases", AppUpdateService.ChangesURL);
    }

    [Theory]
    [InlineData("1.0.0+" + Commit, Commit)]
    [InlineData("1.0.0", null)]
    [InlineData("1.0.0+bad", null)]
    [InlineData(null, null)]
    public void ExtractsOnlyFullCommitFromBuildMetadata(string? version, string? expected) =>
        Assert.Equal(expected, AppUpdateService.CommitFromInformationalVersion(version));

    private static HttpClient Client(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new Handler(_ => new(status) { Content = new StringContent(body) }));

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }
}
