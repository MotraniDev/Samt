using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Samt.Core.Formatting;

namespace Samt_App.Services;

/// <summary>
/// ADR 0001: discover updates via a release manifest JSON (GitHub Releases),
/// notify the user, download only after approval, verify SHA-256, launch installer.
/// </summary>
public sealed class UpdateService
{
    /// <summary>
    /// Default manifest URL (override by publishing at this path on the release tag).
    /// Format: https://github.com/MotraniDev/Samt/releases/latest/download/samt-release.json
    /// </summary>
    public const string DefaultManifestUrl =
        "https://github.com/MotraniDev/Samt/releases/latest/download/samt-release.json";

    private static readonly HttpClient Http = CreateClient();

    public string ManifestUrl { get; set; } = DefaultManifestUrl;

    public async Task<UpdateCheckResult> CheckAsync(bool force = false, CancellationToken ct = default)
    {
        try
        {
            using var response = await Http.GetAsync(ManifestUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var code = (int)response.StatusCode;
                var message = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? App.Localization.Get("UpdateCheckNoRelease")
                    : string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        App.Localization.Get("UpdateCheckFailedDetail"),
                        LatinDigits.EnsureLatin($"HTTP {code}"));
                return UpdateCheckResult.Fail(message, $"HTTP {code}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var manifest = await JsonSerializer.DeserializeAsync<ReleaseManifest>(stream, ManifestJson.Options, ct)
                .ConfigureAwait(false);

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return UpdateCheckResult.Fail(App.Localization.Get("UpdateCheckFailed"), "Invalid manifest.");
            }

            var current = GetCurrentVersion();
            var remote = ParseVersion(manifest.Version);
            if (remote is null)
            {
                return UpdateCheckResult.Fail(App.Localization.Get("UpdateCheckFailed"), "Invalid version.");
            }

            if (remote <= current)
            {
                return new UpdateCheckResult
                {
                    Success = true,
                    UpdateAvailable = false,
                    CurrentVersion = FormatVersion(current),
                    AvailableVersion = FormatVersion(remote),
                    UserMessage = LatinDigits.EnsureLatin(
                        string.Format(App.Localization.Get("UpdateUpToDate"), FormatVersion(current)))
                };
            }

            return new UpdateCheckResult
            {
                Success = true,
                UpdateAvailable = true,
                CurrentVersion = FormatVersion(current),
                AvailableVersion = FormatVersion(remote),
                Manifest = manifest,
                UserMessage = LatinDigits.EnsureLatin(
                    string.Format(
                        App.Localization.Get("UpdateAvailableFormat"),
                        FormatVersion(remote),
                        FormatVersion(current)))
            };
        }
        catch (Exception ex)
        {
            var detail = Truncate(ex.Message, 160);
            var message = string.IsNullOrWhiteSpace(detail)
                ? App.Localization.Get("UpdateCheckFailed")
                : string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    App.Localization.Get("UpdateCheckFailedDetail"),
                    LatinDigits.EnsureLatin(detail));
            return UpdateCheckResult.Fail(message, detail);
        }
    }

    /// <summary>
    /// Downloads the installer from the manifest, verifies SHA-256, and launches it.
    /// Caller must have already obtained user consent.
    /// </summary>
    public async Task<UpdateInstallResult> DownloadAndLaunchAsync(
        ReleaseManifest manifest,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            return UpdateInstallResult.Fail("Missing download URL.");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "SAMT-updates");
        Directory.CreateDirectory(tempDir);
        var fileName = Path.GetFileName(new Uri(manifest.DownloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"SAMT-Setup-{manifest.Version}.exe";
        }

        var targetPath = Path.Combine(tempDir, fileName);

        try
        {
            using var response = await Http.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1L;
            await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var output = File.Create(targetPath);
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                readTotal += read;
                if (total > 0)
                {
                    progress?.Report(readTotal / (double)total);
                }
            }

            await output.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return UpdateInstallResult.Fail(ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            var actual = await ComputeSha256HexAsync(targetPath, ct).ConfigureAwait(false);
            if (!string.Equals(actual, manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(targetPath); } catch { /* ignore */ }
                return UpdateInstallResult.Fail(App.Localization.Get("UpdateHashMismatch"));
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
            return new UpdateInstallResult { Success = true, InstallerPath = targetPath };
        }
        catch (Exception ex)
        {
            return UpdateInstallResult.Fail(ex.Message);
        }
    }

    public static Version GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v ?? new Version(0, 0, 0, 0);
    }

    private static Version? ParseVersion(string text)
    {
        text = text.Trim().TrimStart('v', 'V');
        // Support 2026.7.13 style by padding to 4 parts if needed
        if (Version.TryParse(text, out var v))
        {
            return v;
        }

        var parts = text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is >= 2 and <= 4 && parts.All(p => int.TryParse(p, out _)))
        {
            var nums = parts.Select(int.Parse).ToList();
            while (nums.Count < 4)
            {
                nums.Add(0);
            }

            return new Version(nums[0], nums[1], nums[2], nums[3]);
        }

        return null;
    }

    private static string FormatVersion(Version v)
        => LatinDigits.EnsureLatin($"{v.Major}.{v.Minor}.{v.Build}");

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var t = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return t.Length <= max ? t : t[..max] + "…";
    }

    private static async Task<string> ComputeSha256HexAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SAMT-Windows/1.0 (+https://github.com/MotraniDev/Samt)");
        return client;
    }
}

public sealed class ReleaseManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("minOs")]
    public string? MinOs { get; set; }
}

public sealed class UpdateCheckResult
{
    public bool Success { get; init; }
    public bool UpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = "";
    public string AvailableVersion { get; init; } = "";
    public string UserMessage { get; init; } = "";
    public string? Detail { get; init; }
    public ReleaseManifest? Manifest { get; init; }

    public static UpdateCheckResult Fail(string message, string? detail = null)
        => new()
        {
            Success = false,
            UserMessage = message,
            Detail = detail
        };
}

public sealed class UpdateInstallResult
{
    public bool Success { get; init; }
    public string? InstallerPath { get; init; }
    public string? Error { get; init; }

    public static UpdateInstallResult Fail(string error)
        => new() { Success = false, Error = error };
}

file static class ManifestJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
