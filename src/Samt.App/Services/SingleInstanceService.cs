using System.Threading;
using Samt_App.Helpers;

namespace Samt_App.Services;

/// <summary>
/// Single-instance for unpackaged WinUI using a named Mutex + Event.
/// AppLifecycle AppInstance is unreliable here (stale keys / no Activate callback).
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\SAMT-PrayerApp-SingleInstance";
    private const string ShowEventName = "Local\\SAMT-PrayerApp-ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private CancellationTokenSource? _listenCts;
    private bool _ownsMutex;

    /// <summary>
    /// Try to become the primary instance.
    /// If another instance owns the mutex, signal it to show and return false (caller should exit).
    /// </summary>
    public bool TryClaimAsPrimary()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            _ownsMutex = createdNew;

            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

            if (!createdNew)
            {
                LaunchLog.Write("Single-instance: secondary — signaling primary to show");
                try
                {
                    // Release our non-owned wait immediately if we didn't create.
                    // createdNew=false means another process holds the mutex.
                    _showEvent.Set();
                }
                catch (Exception ex)
                {
                    LaunchLog.Write($"Show signal failed: {ex.Message}");
                }

                // Give primary a moment to wake, then secondary exits.
                Thread.Sleep(200);
                return false;
            }

            LaunchLog.Write("Single-instance: primary (mutex)");
            StartShowListener();
            return true;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"Single-instance unavailable, continuing: {ex.Message}");
            return true;
        }
    }

    private void StartShowListener()
    {
        if (_showEvent is null)
        {
            return;
        }

        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;
        var showEvent = _showEvent;

        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (showEvent.WaitOne(TimeSpan.FromMilliseconds(500)))
                    {
                        LaunchLog.Write("Single-instance: show signal received");
                        App.MainWindow?.DispatcherQueue.TryEnqueue(() => App.ShowMainWindow());
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LaunchLog.Write($"Show listener error: {ex.Message}");
                    Thread.Sleep(500);
                }
            }
        }, token);
    }

    public void Dispose()
    {
        try
        {
            _listenCts?.Cancel();
            _listenCts?.Dispose();
            _showEvent?.Dispose();
            if (_ownsMutex)
            {
                try
                {
                    _mutex?.ReleaseMutex();
                }
                catch
                {
                    // ignore
                }
            }

            _mutex?.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}
