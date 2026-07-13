using Microsoft.Windows.AppLifecycle;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>Ensures only one SAMT process runs; redirects second launches to the first.</summary>
public static class SingleInstanceService
{
    private const string InstanceKey = "SAMT-SingleInstance";

    /// <returns>True if this process should continue; false if it should exit.</returns>
    public static async Task<bool> TryClaimAsync()
    {
        try
        {
            var current = AppInstance.GetCurrent();
            var keyInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
            if (keyInstance.IsCurrent)
            {
                current.Activated += OnActivated;
                LaunchLog.Write("Single-instance: primary");
                return true;
            }

            LaunchLog.Write("Single-instance: redirecting to existing process");
            var args = current.GetActivatedEventArgs();
            await keyInstance.RedirectActivationToAsync(args);
            return false;
        }
        catch (Exception ex)
        {
            // Unpackaged environments may not support AppInstance — continue as multi-instance.
            LaunchLog.Write($"Single-instance unavailable: {ex.Message}");
            return true;
        }
    }

    private static void OnActivated(object? sender, AppActivationArguments e)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            if (App.MainWindow is { } window)
            {
                WindowActivation.ShowCentered(window);
            }
        });
    }
}
