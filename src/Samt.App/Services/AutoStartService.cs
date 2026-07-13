using Microsoft.Win32;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>
/// Unpacks personal install auto-start via HKCU Run (no MSIX StartupTask).
/// Run value launches the current process with <c>--autostart</c> so the shell can hide to tray.
/// </summary>
public static class AutoStartService
{
    public const string RunValueName = "SAMT";
    public const string AutostartArg = "--autostart";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsRequestedFromCommandLine(string[]? args)
        => args is not null
           && args.Any(a => string.Equals(a, AutostartArg, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(a, "/autostart", StringComparison.OrdinalIgnoreCase));

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                LaunchLog.Write("AutoStart: could not open HKCU Run key");
                return;
            }

            if (!enabled)
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
                LaunchLog.Write("AutoStart: Run value removed");
                return;
            }

            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            {
                LaunchLog.Write("AutoStart: ProcessPath missing; skip enable");
                return;
            }

            var command = $"\"{exe}\" {AutostartArg}";
            key.SetValue(RunValueName, command, RegistryValueKind.String);
            LaunchLog.Write($"AutoStart: Run value set → {command}");
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"AutoStart.Apply failed: {ex.Message}");
        }
    }

    /// <summary>True when the Run value exists and points at this process (or any SAMT entry).</summary>
    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(RunValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }
}
