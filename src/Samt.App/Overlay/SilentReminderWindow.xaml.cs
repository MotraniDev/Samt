using Microsoft.UI.Xaml;
using Samt.Core.Formatting;
using Samt_App.Helpers;
using WinRT.Interop;

namespace Samt_App.Overlay;

/// <summary>Small silent in-app reminder card (no audio). Used for calendar reminder delivery.</summary>
public sealed partial class SilentReminderWindow : Window
{
    private readonly DispatcherTimer _autoClose = new() { Interval = TimeSpan.FromSeconds(12) };

    public SilentReminderWindow(string title, string note, string timeLabel)
    {
        InitializeComponent();
        TitleText.Text = LatinDigits.EnsureLatin(title);
        NoteText.Text = LatinDigits.EnsureLatin(note ?? "");
        NoteText.Visibility = string.IsNullOrWhiteSpace(note) ? Visibility.Collapsed : Visibility.Visible;
        TimeText.Text = LatinDigits.EnsureLatin(timeLabel);
        DismissButton.Content = App.Localization?.Get("CalendarClose") ?? "Close";
        if (App.Localization is not null)
        {
            RootBorder.FlowDirection = App.Localization.FlowDirection;
        }

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.IsShownInSwitchers = false;
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.Resize(new Windows.Graphics.SizeInt32(400, 220));
            var display = Microsoft.UI.Windowing.DisplayArea.Primary;
            var work = display.WorkArea;
            appWindow.Move(new Windows.Graphics.PointInt32(
                work.X + work.Width - 420,
                work.Y + work.Height - 260));
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"SilentReminderWindow layout: {ex.Message}");
        }

        _autoClose.Tick += (_, _) =>
        {
            _autoClose.Stop();
            Close();
        };
        _autoClose.Start();
        Closed += (_, _) => _autoClose.Stop();
    }

    private void DismissButton_OnClick(object sender, RoutedEventArgs e) => Close();
}
