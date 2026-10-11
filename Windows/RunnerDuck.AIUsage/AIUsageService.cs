/*
 AIUsageService.cs
 RunnerDuck.AIUsage

 Created by duks93 on 2026/10/06.
 Copyright 2026 Kyome22 (Takuto Nakamura)
 Modified for RunnerDuck by duks93, 2026.

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 */

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RunnerDuck.AIUsage;

public sealed partial class AIUsageService
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private const string ClaudeCodeUserAgent = "claude-cli/2.1.280 (external, cli)";
    private const string GrokAuthKeyPrefix = "https://auth.x.ai::";
    private const string GrokClientVersion = "0.2.93";
    private const int OpenCodexDefaultPort = 10100;

    private static readonly Uri ClaudeCodeUsageURL = new("https://api.anthropic.com/api/oauth/usage");
    private static readonly Uri CodexUsageURL = new("https://chatgpt.com/backend-api/wham/usage");
    private static readonly Uri GrokBillingURL = new("https://cli-chat-proxy.grok.com/v1/billing");
    private static readonly Uri GrokCreditsURL = new("https://cli-chat-proxy.grok.com/v1/billing?format=credits");
    private static readonly string[] ModelLabels = ["Fable", "Opus", "Sonnet"];
    private static readonly JsonSerializerOptions JsonOptions = new() { RespectNullableAnnotations = true };

    private readonly AIUsageEnvironment environment;

    public AIUsageService(AIUsageEnvironment environment)
    {
        this.environment = environment;
    }

    private string OpenCodexDirectory => Path.Combine(environment.HomeDirectory, ".opencodex");

    public IReadOnlyList<AIUsageSource> DetectedSources() =>
        Enum.GetValues<AIUsageSource>().Where(IsDetected).ToList();

    public IReadOnlyList<AIUsageSourceState> SourceStates(AIUsageConfiguration? configuration = null) =>
        Enum.GetValues<AIUsageSource>()
            .Select(source =>
            {
                var isDetected = IsDetected(source);
                return new AIUsageSourceState(source, isDetected, configuration?.IsEnabled(source, isDetected) ?? isDetected);
            })
            .ToList();

    public async Task<IReadOnlyList<AIUsageBundle>> RefreshAsync(
        AIUsageConfiguration? configuration = null,
        CancellationToken cancellationToken = default
    )
    {
        var sources = SourceStates(configuration).Where(state => state.IsEnabled).Select(state => state.Source).ToList();
        var localProviders = sources.Select(source => source.Provider()).OfType<AIUsageProvider>().ToHashSet();
        var results = await Task.WhenAll(sources.Select(source => FetchAsync(source, localProviders, cancellationToken)));
        var bundles = Sorted(results.SelectMany(sourceBundles => sourceBundles));
        environment.Log(
            $"AI usage refreshed: sources=[{string.Join(", ", sources)}] "
            + $"rows=[{string.Join(", ", bundles.Select(bundle => $"{bundle.ID} {LogDescription(bundle)}"))}]"
        );
        return bundles;
    }

    public async Task<IReadOnlyList<AIUsageBundle>> FetchOpenCodexUsageAsync(
        IReadOnlySet<AIUsageProvider>? excludingLoginsOf = null,
        CancellationToken cancellationToken = default
    )
    {
        var baseURL = OpenCodexBaseURL();
        var token = OpenCodexAdminToken();
        var localIdentityHashes = LocalIdentityHashes(excludingLoginsOf ?? new HashSet<AIUsageProvider>());
        (AIUsageProvider Provider, Uri URL)[] requests =
        [
            (AIUsageProvider.ClaudeCode, new Uri(baseURL, "api/oauth/accounts?provider=anthropic&quota=1")),
            (AIUsageProvider.Codex, new Uri(baseURL, "api/codex-auth/accounts")),
            (AIUsageProvider.Grok, new Uri(baseURL, "api/oauth/accounts?provider=xai&quota=1")),
        ];
        var results = await Task.WhenAll(requests.Select(request =>
            FetchOpenCodexAccountsAsync(request.Provider, request.URL, token, cancellationToken)
        ));
        return results
            .SelectMany(providerBundles => providerBundles)
            .Where(bundle => !IsLocalLogin(bundle, localIdentityHashes))
            .ToList();
    }

    public async Task<AIUsageBundle?> FetchUsageAsync(AIUsageProvider provider, CancellationToken cancellationToken = default)
    {
        AIUsageStatus status;
        IReadOnlyList<AIUsageWindow> windows = [];
        try
        {
            windows = provider switch
            {
                AIUsageProvider.ClaudeCode => await FetchClaudeCodeWindowsAsync(cancellationToken),
                AIUsageProvider.Codex => await FetchCodexWindowsAsync(cancellationToken),
                AIUsageProvider.Grok => await FetchGrokWindowsAsync(cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(provider)),
            };
            status = AIUsageStatus.Available;
        }
        catch (UsageException exception) when (exception.Error == UsageError.CredentialsNotFound)
        {
            return null;
        }
        catch (UsageException exception) when (exception.Error == UsageError.Unauthorized)
        {
            status = AIUsageStatus.SignInRequired;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            environment.Log($"Fetching AI usage failed ({provider.RawValue()}): {exception.Message}");
            status = AIUsageStatus.Failed;
            windows = [];
        }
        return new AIUsageBundle(provider, status, windows, environment.Now());
    }

    internal static IReadOnlyList<AIUsageBundle> Sorted(IEnumerable<AIUsageBundle> bundles)
    {
        var list = bundles.ToList();
        return Enum.GetValues<AIUsageProvider>()
            .SelectMany(provider =>
            {
                var providerBundles = list.Where(bundle => bundle.Provider == provider).ToList();
                var localBundles = providerBundles.Where(bundle => bundle.AccountID is null);
                var accountBundles = providerBundles
                    .Where(bundle => bundle.AccountID is not null)
                    .OrderBy(bundle => bundle.AccountName ?? "", StringComparer.Ordinal);
                return localBundles.Concat(accountBundles);
            })
            .ToList();
    }

    internal static bool IsSameIdentity(string openCodexAccountID, string identityHash)
    {
        var baseID = openCodexAccountID.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? openCodexAccountID;
        return baseID.Length >= 8 && identityHash.StartsWith(baseID.ToLowerInvariant(), StringComparison.Ordinal);
    }

    internal static string IdentityHash(string identity) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));

    internal static AIUsageBundle Bundle(
        OpenCodexAccountsResponse.Account account,
        AIUsageProvider provider,
        DateTimeOffset lastUpdatedDate
    )
    {
        var windows = account.AccountQuota is { } quota ? Windows(quota) : [];
        var status = account.NeedsReauth == true
            ? AIUsageStatus.SignInRequired
            : windows.Count == 0 ? AIUsageStatus.Failed : AIUsageStatus.Available;
        return new AIUsageBundle(
            provider,
            status,
            status == AIUsageStatus.Available ? windows : [],
            lastUpdatedDate,
            $"openCodex/{account.ID}",
            account.Alias ?? account.Email ?? "opencodex"
        );
    }

    internal static IReadOnlyList<AIUsageWindow> Windows(OpenCodexAccountsResponse.Quota quota)
    {
        static AIUsageWindow? Window(AIUsageWindowKind kind, FlexibleNumber? percent, FlexibleNumber? resetAt) =>
            NormalizedPercentage(percent?.Value) is { } percentage
                ? new AIUsageWindow(kind, percentage, DateFromEpoch(resetAt?.Value))
                : null;

        var fiveHourPercent = quota.FiveHourPercent?.Value is null ? quota.ShortPercent : quota.FiveHourPercent;
        var fiveHourResetAt = quota.FiveHourResetAt?.Value is null ? quota.ShortResetAt : quota.FiveHourResetAt;
        var standardWindows = new[]
        {
            Window(AIUsageWindowKind.FiveHour, fiveHourPercent, fiveHourResetAt),
            Window(AIUsageWindowKind.Weekly, quota.WeeklyPercent, quota.WeeklyResetAt),
            Window(AIUsageWindowKind.Monthly, quota.MonthlyPercent, quota.MonthlyResetAt),
        };
        var customWindows = (quota.CustomWindows ?? [])
            .Select(custom => Window(AIUsageWindowKind.WeeklyModel(custom.Label), custom.Percent, custom.ResetAt));
        return standardWindows.Concat(customWindows).OfType<AIUsageWindow>().ToList();
    }

    internal static IReadOnlyList<AIUsageWindow> Windows(ClaudeCodeUsageResponse response)
    {
        (AIUsageWindowKind Kind, ClaudeCodeUsageResponse.Bucket? Bucket)[] buckets =
        [
            (AIUsageWindowKind.FiveHour, response.FiveHour),
            (AIUsageWindowKind.Weekly, response.SevenDay),
            (AIUsageWindowKind.WeeklyModel("Fable"), response.SevenDayFable),
            (AIUsageWindowKind.WeeklyModel("Opus"), response.SevenDayOpus),
            (AIUsageWindowKind.WeeklyModel("Sonnet"), response.SevenDaySonnet),
        ];
        var bucketWindows = buckets.Select(entry =>
            NormalizedPercentage(entry.Bucket?.Utilization?.Value) is { } percentage
                ? new AIUsageWindow(entry.Kind, percentage, DateFromString(entry.Bucket?.ResetsAt))
                : null
        );
        var scopedWindows = (response.Limits ?? []).Select(limit =>
            limit.Kind?.ToLowerInvariant() == "weekly_scoped"
                && ModelLabel(limit.Scope?.Model?.DisplayName) is { } label
                && NormalizedPercentage(limit.Percent?.Value) is { } percentage
                ? new AIUsageWindow(AIUsageWindowKind.WeeklyModel(label), percentage, DateFromString(limit.ResetsAt))
                : null
        );
        return bucketWindows
            .Concat(scopedWindows)
            .OfType<AIUsageWindow>()
            .DistinctBy(window => window.Kind)
            .ToList();
    }

    internal static IReadOnlyList<AIUsageWindow> Windows(CodexUsageResponse response)
    {
        if (response.RateLimit is not { } rateLimit)
        {
            return [];
        }
        var rawWindows = new[] { rateLimit.PrimaryWindow, rateLimit.SecondaryWindow, rateLimit.TertiaryWindow };
        var windows = new List<AIUsageWindow>();
        foreach (var rawWindow in rawWindows.OfType<CodexUsageResponse.Window>())
        {
            var kind = Kind(rawWindow.LimitWindowSeconds?.Value);
            if (NormalizedPercentage(rawWindow.UsedPercent?.Value) is not { } percentage
                || windows.Any(window => window.Kind == kind))
            {
                continue;
            }
            windows.Add(new AIUsageWindow(kind, percentage, DateFromEpoch(rawWindow.ResetAt?.Value)));
        }
        return windows;
    }

    internal static AIUsageWindow? WeeklyCreditWindow(GrokBillingResponse response)
    {
        if (response.Config is not { } config
            || config.CurrentPeriod?.Type != "USAGE_PERIOD_TYPE_WEEKLY"
            || NormalizedPercentage(config.CreditUsagePercent?.Value ?? 0) is not { } percentage)
        {
            return null;
        }
        return new AIUsageWindow(AIUsageWindowKind.Weekly, percentage, DateFromFlexible(config.CurrentPeriod?.End));
    }

    internal static AIUsageWindow? MonthlyBillingWindow(GrokBillingResponse response)
    {
        if (response.Config is not { } config
            || config.MonthlyLimit?.Val?.Value is not { } limit || limit <= 0
            || config.Used?.Val?.Value is not { } used
            || NormalizedPercentage(used / limit * 100) is not { } percentage)
        {
            return null;
        }
        return new AIUsageWindow(AIUsageWindowKind.Monthly, percentage, DateFromFlexible(config.BillingPeriodEnd));
    }

    internal static AIUsageWindowKind Kind(double? windowSeconds) => windowSeconds switch
    {
        null or <= 0.0 => AIUsageWindowKind.Weekly,
        < 86_400.0 => AIUsageWindowKind.FiveHour,
        >= 2_419_200.0 => AIUsageWindowKind.Monthly,
        _ => AIUsageWindowKind.Weekly,
    };

    internal static double? NormalizedPercentage(double? value) =>
        value is { } number && double.IsFinite(number) ? Math.Clamp(number, 0, 100) : null;

    internal static DateTimeOffset? DateFromEpoch(double? value)
    {
        if (value is not { } number || !double.IsFinite(number) || number <= 0)
        {
            return null;
        }
        var seconds = number > 10_000_000_000 ? number / 1000 : number;
        return seconds < DateTimeOffset.MaxValue.ToUnixTimeSeconds()
            ? DateTimeOffset.UnixEpoch.AddSeconds(seconds)
            : null;
    }

    internal static DateTimeOffset? DateFromString(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return DateFromEpoch(number);
        }
        return DateTimeOffset.TryParseExact(
            FractionalSeconds().Replace(trimmed, ""),
            "yyyy-MM-dd'T'HH:mm:ssK",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal,
            out var date
        ) ? date : null;
    }

    internal static DateTimeOffset? DateFromFlexible(FlexibleDate? flexibleDate) =>
        DateFromString(flexibleDate?.RawValue) ?? DateFromEpoch(flexibleDate?.Number);

    [GeneratedRegex(@"\.\d+")]
    private static partial Regex FractionalSeconds();

    private static string? ModelLabel(string? displayName)
    {
        var lowercased = displayName?.ToLowerInvariant() ?? "";
        return ModelLabels.FirstOrDefault(label => lowercased.Contains(label.ToLowerInvariant()));
    }

    private static bool IsLocalLogin(AIUsageBundle bundle, IReadOnlyDictionary<AIUsageProvider, string> localIdentityHashes)
    {
        if (!localIdentityHashes.TryGetValue(bundle.Provider, out var identityHash)
            || bundle.AccountID?.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() is not { } accountID)
        {
            return false;
        }
        return IsSameIdentity(accountID, identityHash);
    }

    private static string LogDescription(AIUsageBundle bundle) => bundle.Status switch
    {
        AIUsageStatus.Available => $"available({bundle.Windows.Count})",
        AIUsageStatus.SignInRequired => "signInRequired",
        _ => "failed",
    };

    private async Task<IReadOnlyList<AIUsageBundle>> FetchAsync(
        AIUsageSource source,
        IReadOnlySet<AIUsageProvider> localProviders,
        CancellationToken cancellationToken
    )
    {
        if (source.Provider() is not { } provider)
        {
            return await FetchOpenCodexUsageAsync(localProviders, cancellationToken);
        }
        return await FetchUsageAsync(provider, cancellationToken) is { } bundle ? [bundle] : [];
    }

    private bool IsDetected(AIUsageSource source)
    {
        try
        {
            return source switch
            {
                AIUsageSource.ClaudeCode => LoadClaudeCodeCredentials() is not null,
                AIUsageSource.Codex => LoadCodexTokens() is not null,
                AIUsageSource.Grok => LoadGrokAuthEntry() is not null,
                AIUsageSource.OpenCodex => environment.PathExists(OpenCodexDirectory),
                _ => false,
            };
        }
        catch (UsageException)
        {
            return false;
        }
    }

    private IReadOnlyDictionary<AIUsageProvider, string> LocalIdentityHashes(IReadOnlySet<AIUsageProvider> providers)
    {
        if (!providers.Contains(AIUsageProvider.Grok))
        {
            return new Dictionary<AIUsageProvider, string>();
        }
        try
        {
            return LoadGrokAuthEntry().UserID is { } userID
                ? new Dictionary<AIUsageProvider, string> { [AIUsageProvider.Grok] = IdentityHash(userID) }
                : new Dictionary<AIUsageProvider, string>();
        }
        catch (UsageException)
        {
            return new Dictionary<AIUsageProvider, string>();
        }
    }

    private Uri OpenCodexBaseURL()
    {
        var data = environment.ReadFile(Path.Combine(OpenCodexDirectory, "runtime-port.json"));
        var savedPort = data is null ? null : TryDecode<OpenCodexRuntimePort>(data)?.Port;
        var port = savedPort is >= 1024 and <= 65535 ? savedPort.Value : OpenCodexDefaultPort;
        return new Uri($"http://127.0.0.1:{port}/");
    }

    private string? OpenCodexAdminToken()
    {
        if (environment.ReadFile(Path.Combine(OpenCodexDirectory, "admin-api-token")) is not { } data)
        {
            return null;
        }
        var token = Encoding.UTF8.GetString(data).Trim();
        return token.Length == 0 ? null : token;
    }

    private async Task<IReadOnlyList<AIUsageBundle>> FetchOpenCodexAccountsAsync(
        AIUsageProvider provider,
        Uri url,
        string? token,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var request = Request(url, ("x-opencodex-api-key", token), ("Authorization", token is null ? null : $"Bearer {token}"));
            var response = TryDecode<OpenCodexAccountsResponse>(await SendAsync(request, cancellationToken));
            var now = environment.Now();
            return (response?.Accounts ?? [])
                .Where(account => account.IsMain != true)
                .Select(account => Bundle(account, provider, now))
                .ToList();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<AIUsageWindow>> FetchClaudeCodeWindowsAsync(CancellationToken cancellationToken)
    {
        var oauth = LoadClaudeCodeCredentials().ClaudeAiOauth;
        if (oauth.ExpiresAt is { } expiresAt && DateFromEpoch(expiresAt / 1000) is { } expiryDate && expiryDate < environment.Now())
        {
            throw new UsageException(UsageError.Unauthorized);
        }
        using var request = Request(
            ClaudeCodeUsageURL,
            ("Authorization", $"Bearer {oauth.AccessToken}"),
            ("anthropic-beta", "oauth-2025-04-20"),
            ("User-Agent", ClaudeCodeUserAgent)
        );
        var response = Decode<ClaudeCodeUsageResponse>(await SendAsync(request, cancellationToken));
        return NonEmpty(Windows(response));
    }

    private ClaudeCodeCredentials LoadClaudeCodeCredentials()
    {
        var data = environment.ReadFile(Path.Combine(environment.HomeDirectory, ".claude", ".credentials.json"));
        return (data is null ? null : TryDecode<ClaudeCodeCredentials>(data))
            ?? throw new UsageException(UsageError.CredentialsNotFound);
    }

    private CodexAuth.Tokens LoadCodexTokens()
    {
        var data = environment.ReadFile(Path.Combine(environment.HomeDirectory, ".codex", "auth.json"));
        return (data is null ? null : TryDecode<CodexAuth>(data))?.AuthTokens
            ?? throw new UsageException(UsageError.CredentialsNotFound);
    }

    private async Task<IReadOnlyList<AIUsageWindow>> FetchCodexWindowsAsync(CancellationToken cancellationToken)
    {
        var tokens = LoadCodexTokens();
        using var request = Request(
            CodexUsageURL,
            ("Authorization", $"Bearer {tokens.AccessToken}"),
            ("ChatGPT-Account-Id", tokens.AccountID)
        );
        var response = Decode<CodexUsageResponse>(await SendAsync(request, cancellationToken));
        return NonEmpty(Windows(response));
    }

    private async Task<IReadOnlyList<AIUsageWindow>> FetchGrokWindowsAsync(CancellationToken cancellationToken)
    {
        var entry = LoadGrokAuthEntry();
        if (entry.UserID is { } userID)
        {
            try
            {
                var response = await FetchGrokBillingAsync(GrokCreditsURL, entry, userID, cancellationToken);
                if (WeeklyCreditWindow(response) is { } window)
                {
                    return [window];
                }
                environment.Log("Fetching AI usage failed (grokCredits): unexpected response");
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                environment.Log($"Fetching AI usage failed (grokCredits): {exception.Message}");
            }
        }
        var billing = await FetchGrokBillingAsync(GrokBillingURL, entry, null, cancellationToken);
        return MonthlyBillingWindow(billing) is { } monthlyWindow
            ? [monthlyWindow]
            : throw new UsageException(UsageError.UnexpectedResponse, "status 200");
    }

    private GrokAuthEntry LoadGrokAuthEntry()
    {
        var data = environment.ReadFile(Path.Combine(environment.HomeDirectory, ".grok", "auth.json"));
        var entries = data is null ? null : TryDecode<Dictionary<string, GrokAuthEntry>>(data);
        return entries?.FirstOrDefault(entry => entry.Key.StartsWith(GrokAuthKeyPrefix, StringComparison.Ordinal)).Value
            ?? throw new UsageException(UsageError.CredentialsNotFound);
    }

    private async Task<GrokBillingResponse> FetchGrokBillingAsync(
        Uri url,
        GrokAuthEntry entry,
        string? userID,
        CancellationToken cancellationToken
    )
    {
        using var request = Request(
            url,
            ("Authorization", $"Bearer {entry.Key}"),
            ("x-xai-token-auth", userID is null ? null : "xai-grok-cli"),
            ("x-authenticateresponse", userID is null ? null : "authenticate-response"),
            ("x-userid", userID),
            ("x-grok-client-version", userID is null ? null : GrokClientVersion)
        );
        return Decode<GrokBillingResponse>(await SendAsync(request, cancellationToken));
    }

    private static HttpRequestMessage Request(Uri url, params (string Name, string? Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        foreach (var (name, value) in headers)
        {
            if (value is not null)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }
        return request;
    }

    private async Task<byte[]> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await environment.Http.SendAsync(request, cancellationToken);
        var statusCode = (int)response.StatusCode;
        return statusCode switch
        {
            >= 200 and < 300 => await response.Content.ReadAsByteArrayAsync(cancellationToken),
            401 or 403 => throw new UsageException(UsageError.Unauthorized),
            _ => throw new UsageException(UsageError.UnexpectedResponse, $"status {statusCode}"),
        };
    }

    private static T? TryDecode<T>(byte[] data) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(data, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static T Decode<T>(byte[] data) where T : class =>
        TryDecode<T>(data) ?? throw new UsageException(UsageError.DecodingFailed, typeof(T).Name);

    private static IReadOnlyList<AIUsageWindow> NonEmpty(IReadOnlyList<AIUsageWindow> windows) =>
        windows.Count > 0 ? windows : throw new UsageException(UsageError.NoWindows);

    internal enum UsageError
    {
        CredentialsNotFound,
        Unauthorized,
        UnexpectedResponse,
        DecodingFailed,
        NoWindows,
    }

    internal sealed class UsageException(UsageError error, string? detail = null)
        : Exception(detail is null ? error.ToString() : $"{error}: {detail}")
    {
        public UsageError Error { get; } = error;
    }
}
