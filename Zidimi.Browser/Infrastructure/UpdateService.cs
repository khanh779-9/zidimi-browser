using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Zidimi.Browser.Infrastructure;

/// <summary>Checks the public Zidimi Browser GitHub repository for a newer release/tag.</summary>
public static class UpdateService
{
    private const string Repository = "khanh779-9/zidimi-browser";
    private static readonly HttpClient Client = CreateClient();

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

            var latest = await TryGetLatestReleaseAsync(cancellationToken)
                         ?? await TryGetLatestTagAsync(cancellationToken);

            if (latest is null)
                return UpdateCheckResult.Failed("No release or version tag was found.");

            if (!TryParseVersion(latest.Value.Name, out var latestVersion))
                return UpdateCheckResult.Failed($"Unrecognized version tag: {latest.Value.Name}");

            return new UpdateCheckResult(
                Success: true,
                IsUpdateAvailable: latestVersion > current,
                CurrentVersion: current,
                LatestVersion: latestVersion,
                PageUrl: latest.Value.Url,
                Error: null);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Update", ex, "Checking GitHub for updates.");
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    private static async Task<(string Name, string Url)?> TryGetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(
            $"https://api.github.com/repos/{Repository}/releases/latest", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagNode) ? tagNode.GetString() : null;
        var url = root.TryGetProperty("html_url", out var urlNode) ? urlNode.GetString() : null;
        return string.IsNullOrWhiteSpace(tag)
            ? null
            : (tag, url ?? $"https://github.com/{Repository}/releases");
    }

    private static async Task<(string Name, string Url)?> TryGetLatestTagAsync(CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(
            $"https://api.github.com/repos/{Repository}/tags?per_page=20", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        (string Name, Version Version)? best = null;
        foreach (var tagNode in document.RootElement.EnumerateArray())
        {
            var name = tagNode.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || !TryParseVersion(name, out var version)) continue;

            if (best is null || version > best.Value.Version)
                best = (name, version);
        }

        return best is null
            ? null
            : (best.Value.Name, $"https://github.com/{Repository}/releases/tag/{best.Value.Name}");
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        value = value?.Trim();
        if (!string.IsNullOrEmpty(value) && (value[0] == 'v' || value[0] == 'V'))
            value = value[1..];

        // Strip prerelease/build metadata before System.Version parsing.
        var dash = value?.IndexOfAny(['-', '+']) ?? -1;
        if (dash > 0) value = value![..dash];

        if (Version.TryParse(value, out var parsed))
        {
            version = parsed;
            return true;
        }

        version = new Version(0, 0);
        return false;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Zidimi-Browser-UpdateChecker/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}

public sealed record UpdateCheckResult(
    bool Success,
    bool IsUpdateAvailable,
    Version CurrentVersion,
    Version LatestVersion,
    string? PageUrl,
    string? Error)
{
    public static UpdateCheckResult Failed(string error)
        => new(false, false, new Version(0, 0), new Version(0, 0), null, error);
}
