using Windows.ApplicationModel.Resources;
using Windows.Globalization;
using Microsoft.UI.Xaml;
using Samt.Core.Formatting;

namespace Samt_App.Services;

public sealed class LocalizationService
{
    private ResourceLoader? _loader;

    public string CurrentLanguage { get; private set; } = "ar";

    public event EventHandler? LanguageChanged;

    public void Initialize(string? language = null)
    {
        ApplyLanguage(language ?? CurrentLanguage);
    }

    public void SetLanguage(string language)
    {
        var normalized = Normalize(language);
        if (string.Equals(CurrentLanguage, normalized, StringComparison.OrdinalIgnoreCase))
        {
            // Still re-apply digit culture in case process culture was reset.
            LatinDigits.ApplyProcessDefaults(normalized);
            return;
        }

        ApplyLanguage(normalized);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        try
        {
            _loader ??= new ResourceLoader();
            var value = _loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : LatinDigits.EnsureLatin(value);
        }
        catch
        {
            return key;
        }
    }

    public FlowDirection FlowDirection =>
        CurrentLanguage.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

    private void ApplyLanguage(string language)
    {
        CurrentLanguage = Normalize(language);
        // Resource qualifier uses "ar" folder / "en-US" folder
        ApplicationLanguages.PrimaryLanguageOverride = CurrentLanguage;
        LatinDigits.ApplyProcessDefaults(CurrentLanguage);
        _loader = null;
        _loader = new ResourceLoader();
    }

    private static string Normalize(string language)
        => language is "en" or "en-US" or "en-GB" ? "en-US" : "ar";
}
