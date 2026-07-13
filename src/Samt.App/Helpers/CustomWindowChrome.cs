using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace Samt_App.Helpers;

/// <summary>
/// Removes the system title bar and wires custom min / max / close controls
/// that match SAMT chrome (gold on deep navy).
/// </summary>
internal static class CustomWindowChrome
{
    /// <summary>
    /// Apply borderless custom chrome. Call after InitializeComponent when named parts exist.
    /// </summary>
    /// <param name="dragRegion">Element used as the non-client drag strip (SetTitleBar).</param>
    /// <param name="captionButtons">Optional host for custom caption buttons (excluded from drag).</param>
    public static AppWindow? Apply(
        Window window,
        UIElement dragRegion,
        Button? minimizeButton = null,
        Button? maximizeButton = null,
        Button? closeButton = null,
        SizeInt32? initialSize = null,
        bool allowMaximize = true,
        Action? onCloseClick = null)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            var id = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(id);

            // Hide system title bar; keep a thin border for resize.
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                presenter.IsResizable = true;
                presenter.IsMaximizable = allowMaximize;
                presenter.IsMinimizable = true;
            }

            // Client-area drag + custom buttons (do not call ExtendsContentIntoTitleBar
            // without SetTitleBar — that combination has been unstable on unpackaged runs).
            window.ExtendsContentIntoTitleBar = true;
            window.SetTitleBar(dragRegion);

            if (initialSize is { } size)
            {
                appWindow.Resize(size);
            }

            AppIconHelper.ApplyToAppWindow(appWindow);

            if (minimizeButton is not null)
            {
                minimizeButton.Click += (_, _) =>
                {
                    if (appWindow.Presenter is OverlappedPresenter p)
                    {
                        p.Minimize();
                    }
                };
            }

            if (maximizeButton is not null)
            {
                maximizeButton.Visibility = allowMaximize ? Visibility.Visible : Visibility.Collapsed;
                maximizeButton.Click += (_, _) => ToggleMaximize(appWindow, maximizeButton);
                UpdateMaximizeGlyph(appWindow, maximizeButton);
            }

            if (closeButton is not null)
            {
                // Main shell passes onCloseClick → hide to tray. Calling window.Close()
                // permanently destroys the WinUI Window (tray Open then fails with
                // "Window object has already been closed").
                closeButton.Click += (_, _) =>
                {
                    if (onCloseClick is not null)
                    {
                        onCloseClick();
                    }
                    else
                    {
                        window.Close();
                    }
                };
            }

            appWindow.Changed += (_, args) =>
            {
                if (args.DidPresenterChange && maximizeButton is not null)
                {
                    UpdateMaximizeGlyph(appWindow, maximizeButton);
                }
            };

            return appWindow;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"CustomWindowChrome.Apply failed: {ex.Message}");
            return null;
        }
    }

    public static void StyleCaptionButton(Button button, bool isClose = false)
    {
        button.Width = 40;
        button.Height = 32;
        button.Padding = new Thickness(0);
        button.Background = new SolidColorBrush(Colors.Transparent);
        button.BorderThickness = new Thickness(0);
        button.CornerRadius = new CornerRadius(4);
        if (button.Content is FontIcon icon)
        {
            icon.FontSize = 12;
            icon.Foreground = isClose
                ? new SolidColorBrush(Colors.White)
                : (Brush)Application.Current.Resources["SamtIvoryMutedBrush"];
        }
    }

    private static void ToggleMaximize(AppWindow appWindow, Button maximizeButton)
    {
        if (appWindow.Presenter is not OverlappedPresenter p)
        {
            return;
        }

        if (p.State == OverlappedPresenterState.Maximized)
        {
            p.Restore();
        }
        else
        {
            p.Maximize();
        }

        UpdateMaximizeGlyph(appWindow, maximizeButton);
    }

    private static void UpdateMaximizeGlyph(AppWindow appWindow, Button maximizeButton)
    {
        if (maximizeButton.Content is not FontIcon icon)
        {
            return;
        }

        var maximized = appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        // ChromeRestore / ChromeMaximize
        icon.Glyph = maximized ? "\uE923" : "\uE922";
    }
}
