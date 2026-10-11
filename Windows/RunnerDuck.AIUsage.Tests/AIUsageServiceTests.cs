using System.Globalization;
using static RunnerDuck.AIUsage.Tests.TestEnvironment;

namespace RunnerDuck.AIUsage.Tests;

public sealed class AIUsageServiceTests
{
    private const string ClaudeCodeCredentialsJSON = """
        { "claudeAiOauth": { "accessToken": "claude-token", "expiresAt": 1790000000000 } }
        """;

    private const string CodexAuthJSON = """
        { "tokens": { "access_token": "codex-token", "account_id": "account" } }
        """;

    private const string GrokAuthJSON = """
        { "https://auth.x.ai::client": { "key": "grok-token", "user_id": "user" } }
        """;

    private static DateTimeOffset Date(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);

    [Fact]
    public async Task FetchUsage_claudeCode_maps_buckets_and_scoped_limits_to_windows()
    {
        var handler = new StubHttpHandler(_ => Response(200, """
            {
              "five_hour": { "utilization": 42.5, "resets_at": "2026-09-25T15:00:00.123456+00:00" },
              "seven_day": { "utilization": 120, "resets_at": "2026-09-30T00:00:00Z" },
              "seven_day_opus": null,
              "seven_day_sonnet": { "utilization": null, "resets_at": null },
              "limits": [
                { "kind": "session", "percent": 42.5 },
                { "kind": "weekly_scoped", "percent": 7, "scope": { "model": { "display_name": "Claude Sonnet 5" } } }
              ]
            }
            """));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".claude/.credentials.json"] = ClaudeCodeCredentialsJSON },
            handler: handler
        ));
        var bundle = await sut.FetchUsageAsync(AIUsageProvider.ClaudeCode);
        Assert.Equal(
            new AIUsageBundle(
                AIUsageProvider.ClaudeCode,
                AIUsageStatus.Available,
                [
                    new(AIUsageWindowKind.FiveHour, 42.5, Date("2026-09-25T15:00:00Z")),
                    new(AIUsageWindowKind.Weekly, 100, Date("2026-09-30T00:00:00Z")),
                    new(AIUsageWindowKind.WeeklyModel("Sonnet"), 7),
                ],
                Now
            ),
            bundle
        );
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://api.anthropic.com/api/oauth/usage"), request.RequestUri);
        Assert.Equal("Bearer claude-token", Header(request, "Authorization"));
        Assert.Equal("oauth-2025-04-20", Header(request, "anthropic-beta"));
    }

    [Fact]
    public async Task FetchUsage_claudeCode_returns_signInRequired_without_request_when_token_expired()
    {
        var handler = new StubHttpHandler(_ => Response(200));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".claude/.credentials.json"] = ClaudeCodeCredentialsJSON },
            handler: handler,
            now: DateTimeOffset.FromUnixTimeSeconds(1_800_000_000)
        ));
        var bundle = await sut.FetchUsageAsync(AIUsageProvider.ClaudeCode);
        Assert.Equal(AIUsageStatus.SignInRequired, bundle?.Status);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(AIUsageProvider.ClaudeCode)]
    [InlineData(AIUsageProvider.Codex)]
    [InlineData(AIUsageProvider.Grok)]
    public async Task FetchUsage_returns_null_when_credentials_are_missing(AIUsageProvider provider)
    {
        var sut = new AIUsageService(Make());
        Assert.Null(await sut.FetchUsageAsync(provider));
    }

    [Fact]
    public async Task FetchUsage_codex_classifies_windows_by_declared_duration()
    {
        var handler = new StubHttpHandler(_ => Response(200, """
            {
              "rate_limit": {
                "primary_window": { "used_percent": 12, "reset_at": 1790000000, "limit_window_seconds": 18000 },
                "secondary_window": { "used_percent": 34, "reset_at": 1790500000, "limit_window_seconds": 604800 },
                "tertiary_window": null
              }
            }
            """));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".codex/auth.json"] = CodexAuthJSON },
            handler: handler
        ));
        var bundle = await sut.FetchUsageAsync(AIUsageProvider.Codex);
        Assert.Equal(
            new AIUsageWindow[]
            {
                new(AIUsageWindowKind.FiveHour, 12, DateTimeOffset.FromUnixTimeSeconds(1_790_000_000)),
                new(AIUsageWindowKind.Weekly, 34, DateTimeOffset.FromUnixTimeSeconds(1_790_500_000)),
            },
            bundle?.Windows
        );
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://chatgpt.com/backend-api/wham/usage"), request.RequestUri);
        Assert.Equal("Bearer codex-token", Header(request, "Authorization"));
        Assert.Equal("account", Header(request, "ChatGPT-Account-Id"));
    }

    [Theory]
    [InlineData(401, AIUsageStatus.SignInRequired)]
    [InlineData(403, AIUsageStatus.SignInRequired)]
    [InlineData(500, AIUsageStatus.Failed)]
    public async Task FetchUsage_codex_maps_error_status_codes(int statusCode, AIUsageStatus expected)
    {
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".codex/auth.json"] = CodexAuthJSON },
            handler: new StubHttpHandler(_ => Response(statusCode))
        ));
        Assert.Equal(expected, (await sut.FetchUsageAsync(AIUsageProvider.Codex))?.Status);
    }

    [Fact]
    public async Task FetchUsage_grok_prefers_weekly_credits()
    {
        var handler = new StubHttpHandler(_ => Response(200, """
            {
              "config": {
                "creditUsagePercent": 55.5,
                "currentPeriod": { "type": "USAGE_PERIOD_TYPE_WEEKLY", "end": "2026-09-28T00:00:00Z" }
              }
            }
            """));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".grok/auth.json"] = GrokAuthJSON },
            handler: handler
        ));
        var bundle = await sut.FetchUsageAsync(AIUsageProvider.Grok);
        Assert.Equal(
            new AIUsageWindow[] { new(AIUsageWindowKind.Weekly, 55.5, Date("2026-09-28T00:00:00Z")) },
            bundle?.Windows
        );
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://cli-chat-proxy.grok.com/v1/billing?format=credits"), request.RequestUri);
        Assert.Equal("user", Header(request, "x-userid"));
    }

    [Fact]
    public async Task FetchUsage_grok_falls_back_to_monthly_billing_when_weekly_credits_are_unavailable()
    {
        var handler = new StubHttpHandler(request => string.IsNullOrEmpty(request.RequestUri?.Query)
            ? Response(200, """
                {
                  "config": {
                    "monthlyLimit": { "val": "5000" },
                    "used": { "val": 1250 },
                    "billingPeriodEnd": "2026-10-01T00:00:00Z"
                  }
                }
                """)
            : Response(404));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".grok/auth.json"] = GrokAuthJSON },
            handler: handler
        ));
        var bundle = await sut.FetchUsageAsync(AIUsageProvider.Grok);
        Assert.Equal(
            new AIUsageWindow[] { new(AIUsageWindowKind.Monthly, 25, Date("2026-10-01T00:00:00Z")) },
            bundle?.Windows
        );
    }

    [Fact]
    public async Task Refresh_returns_bundles_in_provider_order()
    {
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string>
            {
                [".grok/auth.json"] = GrokAuthJSON,
                [".codex/auth.json"] = CodexAuthJSON,
            },
            handler: new StubHttpHandler(_ => Response(401))
        ));
        Assert.Equal(
            new AIUsageBundle[]
            {
                new(AIUsageProvider.Codex, AIUsageStatus.SignInRequired, [], Now),
                new(AIUsageProvider.Grok, AIUsageStatus.SignInRequired, [], Now),
            },
            await sut.RefreshAsync()
        );
    }

    [Fact]
    public void DetectedSources_lists_sources_with_credentials()
    {
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string> { [".codex/auth.json"] = """{ "tokens": { "access_token": "token" } }""" },
            pathExists: path => path.EndsWith(".opencodex", StringComparison.Ordinal)
        ));
        Assert.Equal(new[] { AIUsageSource.Codex, AIUsageSource.OpenCodex }, sut.DetectedSources());
    }

    [Theory]
    [InlineData(null, AIUsageWindowKindType.Weekly)]
    [InlineData(18_000.0, AIUsageWindowKindType.FiveHour)]
    [InlineData(604_800.0, AIUsageWindowKindType.Weekly)]
    [InlineData(2_592_000.0, AIUsageWindowKindType.Monthly)]
    public void Kind_classifies_window_duration(double? seconds, AIUsageWindowKindType expected)
    {
        Assert.Equal(expected, AIUsageService.Kind(seconds).Type);
    }
}

public sealed class AIUsageServiceOpenCodexTests
{
    [Fact]
    public async Task FetchOpenCodexUsage_maps_pool_accounts_and_skips_main_account()
    {
        var handler = new StubHttpHandler(request => request.RequestUri?.AbsolutePath == "/api/codex-auth/accounts"
            ? Response(200, """
                {
                  "accounts": [
                    { "id": "main", "email": "main@example.com", "isMain": true, "quota": { "weeklyPercent": 1 } },
                    { "id": "sub", "alias": "sub-1", "email": "sub@example.com",
                      "quota": { "shortPercent": 20, "shortResetAt": 1790000000, "weeklyPercent": 70 } },
                    { "id": "expired", "email": "expired@example.com", "needsReauth": true, "quota": null }
                  ]
                }
                """)
            : Response(200, """{ "accounts": [] }"""));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string>
            {
                [".opencodex/runtime-port.json"] = """{ "port": 10200 }""",
                [".opencodex/admin-api-token"] = "ocx_admin_token\n",
            },
            handler: handler
        ));
        var bundles = await sut.FetchOpenCodexUsageAsync();
        Assert.Equal(
            new AIUsageBundle[]
            {
                new(AIUsageProvider.Codex, AIUsageStatus.SignInRequired, [], Now, "openCodex/expired", "expired@example.com"),
                new(
                    AIUsageProvider.Codex,
                    AIUsageStatus.Available,
                    [
                        new(AIUsageWindowKind.FiveHour, 20, DateTimeOffset.FromUnixTimeSeconds(1_790_000_000)),
                        new(AIUsageWindowKind.Weekly, 70),
                    ],
                    Now,
                    "openCodex/sub",
                    "sub-1"
                ),
            },
            AIUsageService.Sorted(bundles)
        );
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("127.0.0.1", request.RequestUri?.Host);
            Assert.Equal(10200, request.RequestUri?.Port);
            Assert.Equal("ocx_admin_token", Header(request, "x-opencodex-api-key"));
        });
    }

    [Fact]
    public async Task FetchOpenCodexUsage_returns_empty_when_proxy_is_not_running()
    {
        var sut = new AIUsageService(Make());
        Assert.Empty(await sut.FetchOpenCodexUsageAsync());
    }

    [Fact]
    public void Windows_fromOpenCodexQuota_includes_model_scoped_windows()
    {
        var quota = new OpenCodexAccountsResponse.Quota
        {
            FiveHourPercent = new FlexibleNumber(10),
            FiveHourResetAt = new FlexibleNumber(1_790_000_000_000),
            WeeklyPercent = new FlexibleNumber(30),
            CustomWindows = [new() { Label = "Opus", Percent = new FlexibleNumber(50) }],
        };
        Assert.Equal(
            new AIUsageWindow[]
            {
                new(AIUsageWindowKind.FiveHour, 10, DateTimeOffset.FromUnixTimeSeconds(1_790_000_000)),
                new(AIUsageWindowKind.Weekly, 30),
                new(AIUsageWindowKind.WeeklyModel("Opus"), 50),
            },
            AIUsageService.Windows(quota)
        );
    }
}

public sealed class AIUsageServiceIdentityTests
{
    [Fact]
    public async Task FetchOpenCodexUsage_hides_account_matching_local_grok_login()
    {
        var sameID = AIUsageService.IdentityHash("user-1")[..8];
        var handler = new StubHttpHandler(request => request.RequestUri?.Query.Contains("xai") == true
            ? Response(200, $$"""
                { "accounts": [ { "id": "{{sameID}}", "quota": { "weeklyPercent": 1 } }, { "id": "other123", "quota": { "weeklyPercent": 2 } } ] }
                """)
            : Response(200, """{ "accounts": [] }"""));
        var sut = new AIUsageService(Make(
            files: new Dictionary<string, string>
            {
                [".grok/auth.json"] = """{ "https://auth.x.ai::client": { "key": "token", "user_id": "user-1" } }""",
            },
            handler: handler
        ));
        var bundles = await sut.FetchOpenCodexUsageAsync(new HashSet<AIUsageProvider> { AIUsageProvider.Grok });
        Assert.Equal(new[] { "openCodex/other123" }, bundles.Select(bundle => bundle.AccountID));
        Assert.Equal(2, (await sut.FetchOpenCodexUsageAsync()).Count);
    }

    [Theory]
    [InlineData("20b539dd", true)]
    [InlineData("20b539dd-1", true)]
    [InlineData("20b539dd00000000000000000000000f", false)]
    [InlineData("abc", false)]
    [InlineData("ffffffff", false)]
    public void IsSameIdentity_matches_hash_prefix(string accountID, bool expected)
    {
        const string hash = "20b539dd00000000000000000000000000000000000000000000000000000000";
        Assert.Equal(expected, AIUsageService.IsSameIdentity(accountID, hash));
    }
}
