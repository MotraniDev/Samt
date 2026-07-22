using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>
/// DPAPI-protected token store for Google user credentials under LocalAppData\SAMT.
/// Implements <see cref="IDataStore"/> for Google.Apis.Auth file-based flow.
/// </summary>
public sealed class GoogleOAuthTokenStore : IDataStore
{
    private readonly string _directory;

    public GoogleOAuthTokenStore(string? directory = null)
    {
        _directory = directory
                     ?? Path.Combine(
                         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "SAMT",
                         "google-tokens");
        Directory.CreateDirectory(_directory);
    }

    public Task StoreAsync<T>(string key, T value)
    {
        var path = PathFor(key);
        var json = JsonConvert.SerializeObject(value);
        var plain = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(string key)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            return Task.FromResult(default(T)!);
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var plain = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plain);
            var value = JsonConvert.DeserializeObject<T>(json);
            return Task.FromResult(value!);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"GoogleOAuthTokenStore.Get failed: {ex.Message}");
            return Task.FromResult(default(T)!);
        }
    }

    public Task ClearAsync()
    {
        if (!Directory.Exists(_directory))
        {
            return Task.CompletedTask;
        }

        foreach (var file in Directory.EnumerateFiles(_directory))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"GoogleOAuthTokenStore.Clear file failed: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>True when a refresh/access token file exists for the default user key.</summary>
    public async Task<bool> HasUserCredentialAsync(string userKey = "samt-user")
    {
        var token = await GetAsync<TokenResponse>($"Google.Apis.Auth.OAuth2.Responses.TokenResponse-{userKey}")
            .ConfigureAwait(false);
        return token is not null && (!string.IsNullOrEmpty(token.RefreshToken) || !string.IsNullOrEmpty(token.AccessToken));
    }

    private string PathFor(string key)
    {
        var safe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32];
        return Path.Combine(_directory, safe + ".bin");
    }
}
