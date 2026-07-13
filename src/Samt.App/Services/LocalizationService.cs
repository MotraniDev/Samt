using Microsoft.UI.Xaml;
using Samt.Core.Domain;
using Samt.Core.Formatting;

namespace Samt_App.Services;

/// <summary>
/// App strings for ar / en-US.
/// Does not require ApplicationLanguages.PrimaryLanguageOverride (throws on many unpackaged WinUI runs).
/// </summary>
public sealed class LocalizationService
{
    public string CurrentLanguage { get; private set; } = "ar";

    public event EventHandler? LanguageChanged;

    public bool IsArabic =>
        CurrentLanguage.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

    public void Initialize(string? language = null)
    {
        ApplyLanguage(language ?? CurrentLanguage, raiseEvent: false);
    }

    public void SetLanguage(string language)
    {
        var normalized = Normalize(language);
        if (string.Equals(CurrentLanguage, normalized, StringComparison.OrdinalIgnoreCase))
        {
            LatinDigits.ApplyProcessDefaults(normalized);
            return;
        }

        ApplyLanguage(normalized, raiseEvent: true);
    }

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return key ?? string.Empty;
        }

        var map = IsArabic ? Arabic : English;
        if (map.TryGetValue(key, out var value))
        {
            return LatinDigits.EnsureLatin(value);
        }

        // Namespace-style key without prefix match: return key as last resort.
        return key;
    }

    public string GetPrayerName(PrayerEvent prayer)
        => Get($"Prayer.{prayer}");

    public FlowDirection FlowDirection =>
        IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    private void ApplyLanguage(string language, bool raiseEvent)
    {
        CurrentLanguage = Normalize(language);
        LatinDigits.ApplyProcessDefaults(CurrentLanguage);

        // Optional: try system override when available (packaged). Never fail launch if it throws.
        try
        {
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = CurrentLanguage;
        }
        catch
        {
            // Unpackaged WinUI often throws InvalidOperationException here — ignore.
        }

        if (raiseEvent)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string Normalize(string language)
        => language is "en" or "en-US" or "en-GB" ? "en-US" : "ar";

    private static readonly Dictionary<string, string> Arabic = new(StringComparer.Ordinal)
    {
        ["AppDisplayName"] = "سَمت",
        ["AppTagline"] = "مواقيت الصلاة والأذان",
        ["NavToday"] = "اليوم",
        ["NavLocations"] = "المواقع",
        ["NavAlerts"] = "التنبيهات",
        ["NavDiagnostics"] = "التشخيص",
        ["NavDesignLab"] = "مختبر التصميم",
        ["Language"] = "اللغة",
        ["Theme"] = "المظهر",
        ["ThemeSystem"] = "تلقائي",
        ["ThemeLight"] = "فاتح",
        ["ThemeDark"] = "داكن",
        ["Location"] = "الموقع",
        ["Method"] = "طريقة الحساب",
        ["Date"] = "التاريخ",
        ["Recalculate"] = "إعادة الحساب",
        ["Latitude"] = "خط العرض",
        ["Longitude"] = "خط الطول",
        ["TimeZone"] = "المنطقة الزمنية",
        ["Inputs"] = "المدخلات",
        ["Results"] = "مواقيت اليوم",
        ["Disclaimer"] = "هذه تقديرات فلكية محلية وليست مصدراً رسمياً. تحقق من جدول مسجدك. الأرقام تُعرض بالأرقام اللاتينية (0-9).",
        ["Prayer.Imsak"] = "الإمساك",
        ["Prayer.Fajr"] = "الفجر",
        ["Prayer.Sunrise"] = "الشروق",
        ["Prayer.Dhuhr"] = "الظهر",
        ["Prayer.Asr"] = "العصر",
        ["Prayer.Maghrib"] = "المغرب",
        ["Prayer.Isha"] = "العشاء",
        ["Prayer.Midnight"] = "منتصف الليل",
        ["Prayer.Jumuah"] = "الجمعة",
        ["AsrMadhab"] = "مذهب العصر",
        ["AsrStandard"] = "الجمهور",
        ["AsrHanafi"] = "الحنفي",
        ["NextPrayer"] = "الصلاة التالية",
        ["DayComplete"] = "انتهت صلوات اليوم",
        ["CalcStatusFormat"] = "{0} · {1}",
        ["SavedLocations"] = "المواقع المحفوظة",
        ["LocationEditor"] = "تحرير الموقع",
        ["LocationName"] = "الاسم",
        ["UseLocation"] = "تفعيل",
        ["NewLocation"] = "جديد",
        ["DeleteLocation"] = "حذف",
        ["SaveLocation"] = "حفظ",
        ["UseGps"] = "تحديد موقعي",
        ["LocationPrivacyNote"] = "الموقع يبقى على جهازك فقط. يمكنك رفض الإذن وإدخال الإحداثيات يدوياً.",
        ["SelectLocationFirst"] = "اختر موقعاً أولاً.",
        ["LocationActivated"] = "تم تفعيل الموقع.",
        ["NameRequired"] = "الاسم مطلوب.",
        ["InvalidTimeZone"] = "المنطقة الزمنية غير صالحة.",
        ["LocationSaved"] = "تم حفظ الموقع.",
        ["CannotDeleteLastLocation"] = "لا يمكن حذف آخر موقع.",
        ["LocationDeleted"] = "تم حذف الموقع.",
        ["EnterCoordinatesHint"] = "أدخل الاسم والإحداثيات ثم احفظ.",
        ["RequestingLocation"] = "جاري طلب إذن الموقع…",
        ["LocationDenied"] = "رُفض إذن الموقع. استخدم الإدخال اليدوي.",
        ["LocationFailed"] = "تعذر الحصول على الموقع.",
        ["LocationFromGps"] = "تم الحفظ من GPS وتفعيله.",
        ["GpsLocationName"] = "موقعي (GPS)",
        ["InvalidLatitude"] = "خط العرض غير صالح (−90 إلى 90).",
        ["InvalidLongitude"] = "خط الطول غير صالح (−180 إلى 180).",
        ["InvalidFridayTime"] = "وقت الجمعة غير صالح. استخدم HH:mm (مثل 13:30).",
        ["FridaySection"] = "الجمعة",
        ["FridayTimeMode"] = "وقت الجمعة",
        ["FridayFollowDhuhr"] = "يتبع الظهر المحسوب",
        ["FridayFixedTime"] = "وقت ثابت",
        ["FixedFridayTime"] = "الوقت الثابت (محلي)",
        ["SuppressDhuhrOnFriday"] = "تعطيل تنبيه الظهر يوم الجمعة (الجمعة بدلًا منه)",
        ["AlertsStartSection"] = "دخول الصلاة",
        ["AlertsBeforeSection"] = "تنبيه مسبق",
        ["AlertsStartEnabled"] = "تفعيل تنبيه دخول الصلاة",
        ["AlertsBeforeEnabled"] = "تفعيل التنبيه المسبق",
        ["AlertsGeneralOffset"] = "الدقائق العامة قبل الصلاة",
        ["AlertsChannels"] = "القنوات",
        ["ChannelToast"] = "إشعار Windows",
        ["ChannelOverlay"] = "نافذة عائمة",
        ["ChannelAudio"] = "صوت / أذان",
        ["AlertsStartPrayers"] = "صلوات الدخول",
        ["AlertsExceptions"] = "استثناءات لكل صلاة (دقائق)",
        ["AlertsExceptionsHint"] = "اترك الحقل فارغًا لاستخدام القاعدة العامة. قيمة مختلفة تتقدم على العامة. إلغاء التحديد يلغي التنبيه المسبق لتلك الصلاة.",
        ["AlertsPriorityNote"] = "الأولوية: الاستثناء الخاص بالصلاة يتقدم على القاعدة العامة. عند التعارض يبقى تنبيه واحد لكل صلاة.",
        ["AlertsFridayHint"] = "وقت الجمعة وتعطيل الظهر يُضبطان في صفحة المواقع لكل موقع.",
        ["SaveAlerts"] = "حفظ التنبيهات",
        ["AlertsSaved"] = "تم حفظ قواعد التنبيه.",
        ["InvalidOffsetMinutes"] = "أدخل دقائق صحيحة بين 0 و 180.",
        ["OverlayPrayerEntered"] = "دخل وقت الصلاة",
        ["OverlayPreAlertFormat"] = "بعد {0} دقيقة",
        ["OverlayStopAdhan"] = "إيقاف الأذان",
        ["OverlayDismiss"] = "إخفاء",
        ["PhaseBanner"] = "المرحلة 0–5: الهيكل، المحرك، المواقع، اليوم، الإشعارات، الأذان، القواعد",
    };

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["AppDisplayName"] = "SAMT",
        ["AppTagline"] = "Prayer times and adhan",
        ["NavToday"] = "Today",
        ["NavLocations"] = "Locations",
        ["NavAlerts"] = "Alerts",
        ["NavDiagnostics"] = "Diagnostics",
        ["NavDesignLab"] = "Design lab",
        ["Language"] = "Language",
        ["Theme"] = "Theme",
        ["ThemeSystem"] = "System",
        ["ThemeLight"] = "Light",
        ["ThemeDark"] = "Dark",
        ["Location"] = "Location",
        ["Method"] = "Calculation method",
        ["Date"] = "Date",
        ["Recalculate"] = "Recalculate",
        ["Latitude"] = "Latitude",
        ["Longitude"] = "Longitude",
        ["TimeZone"] = "Time zone",
        ["Inputs"] = "Inputs",
        ["Results"] = "Today's times",
        ["Disclaimer"] = "Local astronomical estimates — not an official source. Verify against your mosque. Numbers use Latin digits (0-9).",
        ["Prayer.Imsak"] = "Imsak",
        ["Prayer.Fajr"] = "Fajr",
        ["Prayer.Sunrise"] = "Sunrise",
        ["Prayer.Dhuhr"] = "Dhuhr",
        ["Prayer.Asr"] = "Asr",
        ["Prayer.Maghrib"] = "Maghrib",
        ["Prayer.Isha"] = "Isha",
        ["Prayer.Midnight"] = "Midnight",
        ["Prayer.Jumuah"] = "Jumu'ah",
        ["AsrMadhab"] = "Asr madhab",
        ["AsrStandard"] = "Standard",
        ["AsrHanafi"] = "Hanafi",
        ["NextPrayer"] = "Next prayer",
        ["DayComplete"] = "Day's prayers complete",
        ["CalcStatusFormat"] = "{0} · {1}",
        ["SavedLocations"] = "Saved locations",
        ["LocationEditor"] = "Edit location",
        ["LocationName"] = "Name",
        ["UseLocation"] = "Activate",
        ["NewLocation"] = "New",
        ["DeleteLocation"] = "Delete",
        ["SaveLocation"] = "Save",
        ["UseGps"] = "Use my location",
        ["LocationPrivacyNote"] = "Location stays on this device only. You can deny permission and enter coordinates manually.",
        ["SelectLocationFirst"] = "Select a location first.",
        ["LocationActivated"] = "Location activated.",
        ["NameRequired"] = "Name is required.",
        ["InvalidTimeZone"] = "Invalid time zone.",
        ["LocationSaved"] = "Location saved.",
        ["CannotDeleteLastLocation"] = "Cannot delete the last location.",
        ["LocationDeleted"] = "Location deleted.",
        ["EnterCoordinatesHint"] = "Enter a name and coordinates, then save.",
        ["RequestingLocation"] = "Requesting location permission…",
        ["LocationDenied"] = "Location permission denied. Use manual entry.",
        ["LocationFailed"] = "Could not get location.",
        ["LocationFromGps"] = "Saved and activated from GPS.",
        ["GpsLocationName"] = "My location (GPS)",
        ["InvalidLatitude"] = "Invalid latitude (−90 to 90).",
        ["InvalidLongitude"] = "Invalid longitude (−180 to 180).",
        ["InvalidFridayTime"] = "Invalid Friday time. Use HH:mm (e.g. 13:30).",
        ["FridaySection"] = "Friday",
        ["FridayTimeMode"] = "Jumu'ah time",
        ["FridayFollowDhuhr"] = "Follow calculated Dhuhr",
        ["FridayFixedTime"] = "Fixed local time",
        ["FixedFridayTime"] = "Fixed time (local)",
        ["SuppressDhuhrOnFriday"] = "Suppress Dhuhr alert on Friday (use Jumu'ah instead)",
        ["AlertsStartSection"] = "Prayer start",
        ["AlertsBeforeSection"] = "Pre-alert",
        ["AlertsStartEnabled"] = "Enable prayer-start alerts",
        ["AlertsBeforeEnabled"] = "Enable pre-alerts",
        ["AlertsGeneralOffset"] = "General minutes before prayer",
        ["AlertsChannels"] = "Channels",
        ["ChannelToast"] = "Windows toast",
        ["ChannelOverlay"] = "Overlay",
        ["ChannelAudio"] = "Audio / adhan",
        ["AlertsStartPrayers"] = "Start prayers",
        ["AlertsExceptions"] = "Per-prayer exceptions (minutes)",
        ["AlertsExceptionsHint"] = "Leave empty to use the general offset. A different value overrides general. Uncheck to cancel pre-alert for that prayer.",
        ["AlertsPriorityNote"] = "Priority: a per-prayer exception wins over the general rule. Overlaps collapse to one alert per prayer.",
        ["AlertsFridayHint"] = "Friday time and Dhuhr suppress are set per location on the Locations page.",
        ["SaveAlerts"] = "Save alerts",
        ["AlertsSaved"] = "Notification rules saved.",
        ["InvalidOffsetMinutes"] = "Enter minutes between 0 and 180.",
        ["OverlayPrayerEntered"] = "It is time to pray",
        ["OverlayPreAlertFormat"] = "in {0} min",
        ["OverlayStopAdhan"] = "Stop adhan",
        ["OverlayDismiss"] = "Dismiss",
        ["PhaseBanner"] = "Phase 0–5: shell, engine, locations, today, notifications, adhan, rules",
    };
}
