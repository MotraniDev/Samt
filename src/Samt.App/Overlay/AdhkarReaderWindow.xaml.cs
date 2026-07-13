using Microsoft.UI.Xaml;
using Samt.Core.Adhkar;
using Samt.Core.Formatting;
using WinRT.Interop;

namespace Samt_App.Overlay;

public sealed partial class AdhkarReaderWindow : Window
{
    private AdhkarCollection? _collection;
    private int _index;

    public AdhkarReaderWindow()
    {
        InitializeComponent();
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
            appWindow.Resize(new Windows.Graphics.SizeInt32(480, 640));
            appWindow.Title = "Adhkar";
        }
        catch
        {
            // non-fatal
        }
    }

    public void ShowCollection(AdhkarCollectionKind kind, int startIndex = 0)
    {
        _collection = AdhkarCatalog.Get(kind);
        _index = Math.Clamp(startIndex, 0, Math.Max(0, _collection.Items.Count - 1));
        ApplyLanguageChrome();
        ShowItem();
        Activate();
    }

    private void ApplyLanguageChrome()
    {
        var loc = App.Localization;
        if (Content is FrameworkElement root)
        {
            root.FlowDirection = loc.FlowDirection;
        }

        TitleText.Text = _collection is null ? loc.Get("NavAdhkar") : loc.Get(_collection.TitleKey);
        PrevLabel.Text = loc.Get("AdhkarPrev");
        NextLabel.Text = loc.Get("AdhkarNext");
        PlayLabel.Text = loc.Get("AdhkarPlay");
        // Swap chevrons for RTL so "previous" points toward start edge.
        if (loc.IsRightToLeft)
        {
            PrevIcon.Glyph = "\uE76C";
            NextIcon.Glyph = "\uE76B";
        }
        else
        {
            PrevIcon.Glyph = "\uE76B";
            NextIcon.Glyph = "\uE76C";
        }
    }

    private void ShowItem()
    {
        if (_collection is null || _collection.Items.Count == 0)
        {
            ArabicText.Text = string.Empty;
            TranslationText.Text = string.Empty;
            ProgressText.Text = string.Empty;
            return;
        }

        var item = _collection.Items[_index];
        ArabicText.Text = item.ArabicText;
        TranslationText.Text = item.TranslationKey is null
            ? string.Empty
            : App.Localization.Get(item.TranslationKey);

        if (item.RepeatCount is { } n and > 1)
        {
            RepeatText.Visibility = Visibility.Visible;
            RepeatText.Text = LatinDigits.EnsureLatin($"× {n}");
        }
        else
        {
            RepeatText.Visibility = Visibility.Collapsed;
        }

        ProgressText.Text = LatinDigits.EnsureLatin(
            string.Format(
                App.Localization.Get("AdhkarProgressFormat"),
                _index + 1,
                _collection.Items.Count));

        PrevButton.IsEnabled = _index > 0;
        NextButton.IsEnabled = _index < _collection.Items.Count - 1;
    }

    private void PrevButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_index <= 0)
        {
            return;
        }

        _index--;
        ShowItem();
    }

    private void NextButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_collection is null || _index >= _collection.Items.Count - 1)
        {
            return;
        }

        _index++;
        ShowItem();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        => Close();
}
