using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Samt.Core.Domain;
using Samt_App.Helpers;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace Samt_App.Overlay;

public enum OverlayVisualStyle
{
    TopRibbon = 0,
    BottomDock = 1
}

/// <summary>
/// Borderless always-on-top prayer overlay.
/// Solid navy host + layered window alpha. Entrance animates the HWND position + alpha
/// (card-only storyboards are invisible when the host is sized tightly to the card).
/// </summary>
public sealed partial class OverlayWindow : Window
{
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNomove = 0x0002;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpShowwindow = 0x0040;
    private const uint LwaAlpha = 0x00000002;

    private static readonly global::Windows.UI.Color NavyDeep =
        global::Windows.UI.Color.FromArgb(255, 0x07, 0x15, 0x25);
    private static readonly global::Windows.UI.Color Navy =
        global::Windows.UI.Color.FromArgb(255, 0x0B, 0x1F, 0x33);

    private AppWindow? _appWindow;
    private IntPtr _hwnd;
    private int _animationMs = 280;
    private double _targetOpacity = 0.94;
    private bool _reduceMotion;
    private OverlayVisualStyle _style = OverlayVisualStyle.BottomDock;
    private OverlayEdge _entryEdge = OverlayEdge.Bottom;
    private bool _chromeReady;
    private RectInt32 _finalRect;
    private int _motionGen;

    public event EventHandler? StopRequested;

    public OverlayWindow()
    {
        InitializeComponent();
        SystemBackdrop = null;

        Activated += OnActivatedOnce;
        RootGrid.KeyDown += RootGrid_OnKeyDown;
        RootGrid.IsTabStop = true;
    }

    public void Configure(
        string prayerName,
        string timeText,
        string subtitle,
        string stopLabel,
        OverlayVisualStyle style,
        OverlayEdge entryEdge,
        FlowDirection flowDirection,
        double opacity,
        int animationMs,
        bool reduceMotion)
    {
        EnsureChrome();

        TitleText.Text = prayerName;
        TimeText.Text = timeText;
        SubtitleText.Text = subtitle;
        StopButton.Content = stopLabel;
        RootGrid.FlowDirection = flowDirection;
        _style = style;
        _entryEdge = entryEdge;
        _targetOpacity = Math.Clamp(opacity, 0.30, 1.0);
        _animationMs = Math.Clamp(animationMs, 80, 1200);
        _reduceMotion = reduceMotion;

        // Card stays fully visible; motion is on the window itself.
        Card.Opacity = 1;
        CardTransform.TranslateX = 0;
        CardTransform.TranslateY = 0;

        ApplyStyleLayout();
        _finalRect = ComputeWorkAreaRect();
        ApplyWindowAlpha(_targetOpacity);
    }

    public void ShowTopmost()
    {
        EnsureChrome();
        _finalRect = ComputeWorkAreaRect();
        ApplyBorderlessChrome(_hwnd);

        try
        {
            if (_reduceMotion)
            {
                _appWindow?.MoveAndResize(_finalRect);
                ApplyWindowAlpha(_targetOpacity);
                _appWindow?.Show(false);
                if (_hwnd != IntPtr.Zero)
                {
                    ShowWindow(_hwnd, SwShowNoActivate);
                }

                ApplyBorderlessChrome(_hwnd);
                ApplyWindowAlpha(_targetOpacity);
                ApplyTopmost();
            }
            else
            {
                var start = OffsetRect(_finalRect, slideIn: true);
                var startAlpha = Math.Max(0.15, _targetOpacity * 0.25);
                // Place off-stage via Win32 — more reliable mid-animation than AppWindow alone.
                MoveHwnd(start);
                ApplyWindowAlpha(startAlpha);
                _appWindow?.Show(false);
                if (_hwnd != IntPtr.Zero)
                {
                    ShowWindow(_hwnd, SwShowNoActivate);
                }

                ApplyBorderlessChrome(_hwnd);
                ApplyTopmost();
                MoveHwnd(start);
                ApplyWindowAlpha(startAlpha);
                AnimateWindow(start, _finalRect, startAlpha, _targetOpacity, _animationMs);
            }
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"OverlayWindow show: {ex.Message}");
            try
            {
                _appWindow?.MoveAndResize(_finalRect);
                ApplyWindowAlpha(_targetOpacity);
                _appWindow?.Show(false);
            }
            catch
            {
                // ignore
            }
        }

        RootGrid.Focus(FocusState.Programmatic);
        LaunchLog.Write(
            $"Overlay show style={_style} edge={_entryEdge} opacity={_targetOpacity:0.##} anim={_animationMs}ms reduce={_reduceMotion}");
    }

    public void HideOverlay(bool immediate = false)
    {
        CancelMotion();
        if (immediate || _reduceMotion)
        {
            ApplyHide();
            return;
        }

        var start = _appWindow?.Position is { } p && _appWindow.Size is { } s
            ? new RectInt32(p.X, p.Y, s.Width, s.Height)
            : _finalRect;
        var end = OffsetRect(start, slideIn: false);
        AnimateWindow(
            start,
            end,
            _targetOpacity,
            0.05,
            Math.Max(100, (int)(_animationMs * 0.55)),
            hideWhenDone: true);
    }

    private void ApplyHide()
    {
        CancelMotion();
        try
        {
            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SwHide);
            }

            _appWindow?.Hide();
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"OverlayWindow hide: {ex.Message}");
        }
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
        => StopRequested?.Invoke(this, EventArgs.Empty);

    private void RootGrid_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            StopRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnActivatedOnce(object sender, WindowActivatedEventArgs args)
        => EnsureChrome();

    private void EnsureChrome()
    {
        if (_chromeReady && _appWindow is not null)
        {
            return;
        }

        try
        {
            _hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.IsShownInSwitchers = false;
            _appWindow.Title = "SAMT";

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            ApplyBorderlessChrome(_hwnd);
            _chromeReady = true;
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"OverlayWindow chrome failed: {ex.Message}");
        }
    }

    private void ApplyStyleLayout()
    {
        var color = _style == OverlayVisualStyle.TopRibbon ? Navy : NavyDeep;
        CardBg.Color = color;
        RootGrid.Background = new SolidColorBrush(color);
        Card.CornerRadius = _style == OverlayVisualStyle.TopRibbon
            ? new CornerRadius(14)
            : new CornerRadius(18);
    }

    private RectInt32 ComputeWorkAreaRect()
    {
        if (_appWindow is null)
        {
            return new RectInt32(100, 100, 400, 120);
        }

        try
        {
            var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            var work = display.WorkArea;
            var scale = 1.0;
            try
            {
                if (Content is FrameworkElement { XamlRoot: { } xr })
                {
                    scale = xr.RasterizationScale;
                }
            }
            catch
            {
                scale = 1.0;
            }

            int widthDip;
            int heightDip;
            if (_style == OverlayVisualStyle.TopRibbon)
            {
                widthDip = 440;
                heightDip = 120;
            }
            else
            {
                widthDip = Math.Min(680, Math.Max(380, (int)(work.Width / scale) - 64));
                heightDip = 132;
            }

            var width = Math.Max(280, (int)Math.Round(widthDip * scale));
            var height = Math.Max(100, (int)Math.Round(heightDip * scale));
            width = Math.Min(width, work.Width - 16);
            height = Math.Min(height, Math.Max(120, work.Height / 4));

            var x = work.X + Math.Max(0, (work.Width - width) / 2);
            var y = _style == OverlayVisualStyle.TopRibbon
                ? work.Y + (int)Math.Round(24 * scale)
                : work.Y + work.Height - height - (int)Math.Round(36 * scale);

            if (_entryEdge is OverlayEdge.Left or OverlayEdge.Right)
            {
                var margin = (int)Math.Round(16 * scale);
                x = _entryEdge == OverlayEdge.Left
                    ? work.X + margin
                    : work.X + work.Width - width - margin;
                y = work.Y + Math.Max(0, (work.Height - height) / 2);
            }

            return new RectInt32(x, y, width, height);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ComputeWorkAreaRect: {ex.Message}");
            return new RectInt32(100, 100, 400, 120);
        }
    }

    private RectInt32 OffsetRect(RectInt32 rect, bool slideIn)
    {
        // Distance the window travels (pixels). Slightly longer + ease-out reads as soft toast motion.
        const int dist = 110;
        var sign = slideIn ? 1 : 1; // same offset direction for both start (off-stage) and exit
        // For slideIn: start is off-stage. For exit: end is off-stage.
        // Top: off-stage is above (smaller Y). Bottom: below (larger Y).
        var dx = 0;
        var dy = 0;
        switch (_entryEdge)
        {
            case OverlayEdge.Bottom:
                dy = dist * sign;
                break;
            case OverlayEdge.Left:
                dx = -dist * sign;
                break;
            case OverlayEdge.Right:
                dx = dist * sign;
                break;
            default: // Top
                dy = -dist * sign;
                break;
        }

        return new RectInt32(rect.X + dx, rect.Y + dy, rect.Width, rect.Height);
    }

    private void AnimateWindow(
        RectInt32 from,
        RectInt32 to,
        double alphaFrom,
        double alphaTo,
        int durationMs,
        bool hideWhenDone = false)
    {
        CancelMotion();
        if (_hwnd == IntPtr.Zero && _appWindow is null)
        {
            return;
        }

        // Prefer the overlay window's dispatcher so ticks run even if focus stays on main.
        var dq = Content is FrameworkElement fe
            ? fe.DispatcherQueue
            : Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        if (dq is null)
        {
            MoveHwnd(to);
            ApplyWindowAlpha(alphaTo);
            if (hideWhenDone)
            {
                ApplyHide();
            }

            return;
        }

        var gen = ++_motionGen;
        // ~60fps for smooth ease curves
        var steps = Math.Max(16, (int)Math.Ceiling(durationMs / 16.0));
        var step = 0;
        var interval = TimeSpan.FromMilliseconds(Math.Max(12, durationMs / (double)steps));
        var timer = dq.CreateTimer();
        timer.IsRepeating = true;
        timer.Interval = interval;

        LaunchLog.Write(
            $"AnimateWindow ease from=({from.X},{from.Y}) to=({to.X},{to.Y}) steps={steps} dur={durationMs}ms exit={hideWhenDone}");

        timer.Tick += (_, _) =>
        {
            if (gen != _motionGen)
            {
                timer.Stop();
                return;
            }

            step++;
            var t = Math.Clamp(step / (double)steps, 0, 1);
            // Soft cubic-bezier ease: out for entrance, in for dismiss.
            var posEased = hideWhenDone ? MotionEasing.EaseIn(t) : MotionEasing.EaseOut(t);
            var alphaEased = hideWhenDone ? MotionEasing.EaseIn(t) : MotionEasing.EaseOutOpacity(t);

            var x = (int)Math.Round(from.X + (to.X - from.X) * posEased);
            var y = (int)Math.Round(from.Y + (to.Y - from.Y) * posEased);
            var w = Math.Max(1, (int)Math.Round(from.Width + (to.Width - from.Width) * posEased));
            var h = Math.Max(1, (int)Math.Round(from.Height + (to.Height - from.Height) * posEased));
            var alpha = alphaFrom + (alphaTo - alphaFrom) * alphaEased;

            try
            {
                MoveHwnd(new RectInt32(x, y, w, h));
                ApplyWindowAlpha(alpha);
            }
            catch (Exception ex)
            {
                LaunchLog.Write($"AnimateWindow tick: {ex.Message}");
            }

            if (step >= steps)
            {
                timer.Stop();
                if (gen == _motionGen)
                {
                    try
                    {
                        MoveHwnd(to);
                        ApplyWindowAlpha(alphaTo);
                    }
                    catch
                    {
                        // ignore
                    }

                    if (hideWhenDone)
                    {
                        ApplyHide();
                    }
                }
            }
        };

        timer.Start();
    }

    private void MoveHwnd(RectInt32 rect)
    {
        if (_hwnd != IntPtr.Zero)
        {
            // SWP_NOACTIVATE | SWP_SHOWWINDOW — move without stealing focus.
            SetWindowPos(
                _hwnd,
                HwndTopmost,
                rect.X,
                rect.Y,
                Math.Max(1, rect.Width),
                Math.Max(1, rect.Height),
                SwpNoactivate | SwpShowwindow);
        }

        try
        {
            _appWindow?.MoveAndResize(rect);
        }
        catch
        {
            // HWND path is primary.
        }
    }

    private void CancelMotion()
    {
        // Bump generation so any in-flight DispatcherQueueTimer ticks no-op.
        _motionGen++;
    }

    private void ApplyTopmost()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNoactivate | SwpShowwindow);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"OverlayWindow topmost: {ex.Message}");
        }
    }

    private void ApplyWindowAlpha(double opacity)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var alpha = (byte)Math.Clamp((int)Math.Round(Math.Clamp(opacity, 0.05, 1.0) * 255), 1, 255);
            var ex = GetWindowLong(_hwnd, GwlExStyle);
            if ((ex & WsExLayered) == 0)
            {
                SetWindowLong(_hwnd, GwlExStyle, ex | WsExLayered);
            }

            SetLayeredWindowAttributes(_hwnd, 0, alpha, LwaAlpha);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ApplyWindowAlpha: {ex.Message}");
        }
    }

    private static void ApplyBorderlessChrome(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var style = GetWindowLong(hwnd, GwlStyle);
            style &= ~(WsCaption | WsThickFrame | WsBorder | WsDlgFrame | WsMinimizeBox | WsMaximizeBox | WsSystemMenu);
            SetWindowLong(hwnd, GwlStyle, style);

            var ex = GetWindowLong(hwnd, GwlExStyle);
            ex &= ~(WsExClientEdge | WsExWindowEdge | WsExDlgModalFrame | WsExStaticEdge);
            ex |= WsExLayered;
            SetWindowLong(hwnd, GwlExStyle, ex);

            var colorNone = unchecked((int)0xFFFFFFFE);
            DwmSetWindowAttribute(hwnd, 34, ref colorNone, sizeof(int));

            var navyColorRef = 0x00251507;
            DwmSetWindowAttribute(hwnd, 35, ref navyColorRef, sizeof(int));

            var corner = 2;
            DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));

            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SwpNomove | SwpNosize | SwpNoactivate | SwpFrameChanged | SwpNozorder);
        }
        catch (Exception ex)
        {
            LaunchLog.Write($"ApplyBorderlessChrome: {ex.Message}");
        }
    }

    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int WsBorder = 0x00800000;
    private const int WsDlgFrame = 0x00400000;
    private const int WsCaption = 0x00C00000;
    private const int WsThickFrame = 0x00040000;
    private const int WsSystemMenu = 0x00080000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsMaximizeBox = 0x00010000;
    private const int WsExClientEdge = 0x00000200;
    private const int WsExWindowEdge = 0x00000100;
    private const int WsExDlgModalFrame = 0x00000001;
    private const int WsExStaticEdge = 0x00020000;
    private const int WsExLayered = 0x00080000;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNozorder = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static int GetWindowLong(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex).ToInt32()
            : GetWindowLong32(hWnd, nIndex);

    private static void SetWindowLong(IntPtr hWnd, int nIndex, int value)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hWnd, nIndex, new IntPtr(value));
        }
        else
        {
            SetWindowLong32(hWnd, nIndex, value);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
