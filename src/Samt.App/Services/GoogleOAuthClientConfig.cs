using System.Text.Json;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>
/// Loads Google OAuth Desktop client credentials (not user tokens).
/// Sources (first hit wins): env SAMT_GOOGLE_CLIENT_ID/SECRET, then %LocalAppData%\SAMT\google-oauth-client.json.
/// </summary>
public static class GoogleOAuthClientConfig
{
    public const string ClientFileName = "google-oauth-client.json";

    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SAMT");

    public static string ClientFilePath => Path.Combine(DataDirectory, ClientFileName);

    public static bool TryLoad(out string clientId, out string clientSecret, out string? error)
    {
        clientId = "";
        clientSecret = "";
        error = null;

        var envId = Environment.GetEnvironmentVariable("SAMT_GOOGLE_CLIENT_ID");
        var envSecret = Environment.GetEnvironmentVariable("SAMT_GOOGLE_CLIENT_SECRET");
        if (!string.IsNullOrWhiteSpace(envId) && !string.IsNullOrWhiteSpace(envSecret))
        {
            clientId = envId.Trim();
            clientSecret = envSecret.Trim();
            return true;
        }

        try
        {
            if (!File.Exists(ClientFilePath))
            {
                error = "missing-client-file";
                return false;
            }

            var json = File.ReadAllText(ClientFilePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // Support both flat and Google "installed" download shape.
            if (root.TryGetProperty("installed", out var installed))
            {
                root = installed;
            }

            clientId = root.TryGetProperty("client_id", out var idEl) ? idEl.GetString()?.Trim() ?? "" : "";
            clientSecret = root.TryGetProperty("client_secret", out var secEl) ? secEl.GetString()?.Trim() ?? "" : "";
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                error = "invalid-client-file";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"GoogleOAuthClientConfig: {ex.Message}");
            error = "invalid-client-file";
            return false;
        }
    }
}
