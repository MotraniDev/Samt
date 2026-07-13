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
        ["NavAdhkar"] = "الأذكار",
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
        ["ChannelAudioCue"] = "صوت",
        ["BeforeAudioHint"] = "عند التفعيل يُشغَّل صوت التنبيه المسبق المختار من مكتبة الأصوات أدناه.",
        ["ManualTimeBadge"] = "يدوي",
        ["EditPrayerTime"] = "تعديل الوقت",
        ["ResetPrayerTime"] = "إعادة الحساب",
        ["PrayerTimeSaved"] = "تم حفظ التعديل اليدوي.",
        ["InvalidPrayerTime"] = "أدخل وقتًا بصيغة HH:mm (مثل 05:30).",
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
        ["Qibla"] = "القبلة",
        ["QiblaBearingFormat"] = "{0}° · {1}",
        ["QiblaKm"] = "كم",
        ["QiblaAtKaaba"] = "عند الكعبة",
        ["RamadanBadge"] = "رمضان",
        ["Prayer.MaghribIftar"] = "المغرب (الإفطار)",
        ["HijriDayOffset"] = "إزاحة اليوم الهجري",
        ["HijriDayOffsetHint"] = "من −3 إلى 3 لضبط الرؤية المحلية",
        ["Hijri.Month.1"] = "محرم",
        ["Hijri.Month.2"] = "صفر",
        ["Hijri.Month.3"] = "ربيع الأول",
        ["Hijri.Month.4"] = "ربيع الآخر",
        ["Hijri.Month.5"] = "جمادى الأولى",
        ["Hijri.Month.6"] = "جمادى الآخرة",
        ["Hijri.Month.7"] = "رجب",
        ["Hijri.Month.8"] = "شعبان",
        ["Hijri.Month.9"] = "رمضان",
        ["Hijri.Month.10"] = "شوال",
        ["Hijri.Month.11"] = "ذو القعدة",
        ["Hijri.Month.12"] = "ذو الحجة",
        ["Compass.N"] = "ش",
        ["Compass.NE"] = "ش ر",
        ["Compass.E"] = "ر",
        ["Compass.SE"] = "ج ر",
        ["Compass.S"] = "ج",
        ["Compass.SW"] = "ج غ",
        ["Compass.W"] = "غ",
        ["Compass.NW"] = "ش غ",
        ["AdhkarSubtitle"] = "نصوص قصيرة أوفلاين — ليست مكتبة كاملة.",
        ["AdhkarDisclaimer"] = "نصوص تذكيرية مختصرة. راجع مصادرك الموثوقة للتلاوة الكاملة. الأرقام لاتينية 0-9.",
        ["Adhkar.AfterPrayer"] = "بعد الصلاة",
        ["Adhkar.AfterPrayer.1.Arabic"] = "أَسْتَغْفِرُ اللَّهَ (ثلاثًا)",
        ["Adhkar.AfterPrayer.1.Translation"] = "أستغفر الله (ثلاث مرات)",
        ["Adhkar.AfterPrayer.2.Arabic"] = "اللَّهُمَّ أَنْتَ السَّلَامُ وَمِنْكَ السَّلَامُ، تَبَارَكْتَ يَا ذَا الْجَلَالِ وَالْإِكْرَامِ",
        ["Adhkar.AfterPrayer.2.Translation"] = "اللهم أنت السلام ومنك السلام، تباركت يا ذا الجلال والإكرام",
        ["Adhkar.AfterPrayer.3.Arabic"] = "سُبْحَانَ اللَّهِ · الْحَمْدُ لِلَّهِ · اللَّهُ أَكْبَرُ",
        ["Adhkar.AfterPrayer.3.Translation"] = "سبحان الله · الحمد لله · الله أكبر",
        ["Adhkar.Morning"] = "أذكار الصباح",
        ["Adhkar.Morning.1.Arabic"] = "أَصْبَحْنَا وَأَصْبَحَ الْمُلْكُ لِلَّهِ، وَالْحَمْدُ لِلَّهِ",
        ["Adhkar.Morning.1.Translation"] = "أصبحنا وأصبح الملك لله، والحمد لله",
        ["Adhkar.Morning.2.Arabic"] = "بِسْمِ اللَّهِ الَّذِي لَا يَضُرُّ مَعَ اسْمِهِ شَيْءٌ فِي الْأَرْضِ وَلَا فِي السَّمَاءِ وَهُوَ السَّمِيعُ الْعَلِيمُ",
        ["Adhkar.Morning.2.Translation"] = "بسم الله الذي لا يضر مع اسمه شيء في الأرض ولا في السماء وهو السميع العليم",
        ["Adhkar.Evening"] = "أذكار المساء",
        ["Adhkar.Evening.1.Arabic"] = "أَمْسَيْنَا وَأَمْسَى الْمُلْكُ لِلَّهِ، وَالْحَمْدُ لِلَّهِ",
        ["Adhkar.Evening.1.Translation"] = "أمسينا وأمسى الملك لله، والحمد لله",
        ["Adhkar.Evening.2.Arabic"] = "أَعُوذُ بِكَلِمَاتِ اللَّهِ التَّامَّاتِ مِنْ شَرِّ مَا خَلَقَ",
        ["Adhkar.Evening.2.Translation"] = "أعوذ بكلمات الله التامات من شر ما خلق",
        ["AppOptions"] = "خيارات التطبيق",
        ["AutoStartEnabled"] = "بدء التشغيل مع Windows",
        ["ShowMissedAlertOnResume"] = "تنبيه عند فوات تنبيه (استئناف / تشغيل متأخر)",
        ["ToggleOn"] = "تشغيل",
        ["ToggleOff"] = "إيقاف",
        ["RefreshProcessStatus"] = "تحديث حالة العملية",
        ["ProcessStatusFormat"] = "الذاكرة العاملة: {0} ميغابايت · أحداث مجدولة متبقية: {1} · بدء التشغيل: {2}",
        ["AutoStartRegisteredYes"] = "مسجّل",
        ["AutoStartRegisteredNo"] = "غير مسجّل",
        ["MissedAlertTitle"] = "فات تنبيه صلاة",
        ["MissedAlertBodyOne"] = "فات تنبيه {0} ({1}). لا يُعاد تشغيل الأذان المتأخر.",
        ["MissedAlertBodyMany"] = "فاتت {0} تنبيهات: {1}. لا يُعاد تشغيل الأذان المتأخر.",
        ["SoundLibrary"] = "مكتبة الأصوات",
        ["SoundLibraryHint"] = "اختر أذان دخول الصلاة، وصوت التنبيه المسبق (تكبير / حي على الصلاة / نغمة). يمكنك إضافة ملفاتك.",
        ["AdhanSound"] = "صوت الأذان (دخول الصلاة)",
        ["PreAlertSound"] = "صوت التنبيه المسبق",
        ["PreviewSound"] = "استماع",
        ["AddSound"] = "إضافة صوت…",
        ["StopSound"] = "إيقاف",
        ["SoundLibraryNote"] = "عينات الأذان من PrayTimes.org للاستخدام الشخصي. عبارات التكبير وحي على الصلاة نغمات قصيرة قابلة للاستبدال. تحقق من الترخيص قبل أي نشر عام.",
        ["SoundAdded"] = "أُضيف الصوت إلى المكتبة.",
        ["SoundAddFailed"] = "تعذّر إضافة الصوت:",
        ["PhaseBanner"] = "المرحلة 0–7: الهيكل حتى الصقل والتسليم الشخصي",
    };

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["AppDisplayName"] = "SAMT",
        ["AppTagline"] = "Prayer times and adhan",
        ["NavToday"] = "Today",
        ["NavLocations"] = "Locations",
        ["NavAlerts"] = "Alerts",
        ["NavAdhkar"] = "Adhkar",
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
        ["ChannelAudioCue"] = "Sound",
        ["BeforeAudioHint"] = "When enabled, plays the pre-alert sound selected in the sound library below.",
        ["ManualTimeBadge"] = "Manual",
        ["EditPrayerTime"] = "Edit time",
        ["ResetPrayerTime"] = "Reset calculated",
        ["PrayerTimeSaved"] = "Manual adjustment saved.",
        ["InvalidPrayerTime"] = "Enter a time as HH:mm (e.g. 05:30).",
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
        ["Qibla"] = "Qibla",
        ["QiblaBearingFormat"] = "{0}° · {1}",
        ["QiblaKm"] = "km",
        ["QiblaAtKaaba"] = "At the Kaaba",
        ["RamadanBadge"] = "Ramadan",
        ["Prayer.MaghribIftar"] = "Maghrib (Iftar)",
        ["HijriDayOffset"] = "Hijri day offset",
        ["HijriDayOffsetHint"] = "−3 to 3 to match local moon sighting",
        ["Hijri.Month.1"] = "Muharram",
        ["Hijri.Month.2"] = "Safar",
        ["Hijri.Month.3"] = "Rabi' al-Awwal",
        ["Hijri.Month.4"] = "Rabi' al-Thani",
        ["Hijri.Month.5"] = "Jumada al-Ula",
        ["Hijri.Month.6"] = "Jumada al-Akhira",
        ["Hijri.Month.7"] = "Rajab",
        ["Hijri.Month.8"] = "Sha'ban",
        ["Hijri.Month.9"] = "Ramadan",
        ["Hijri.Month.10"] = "Shawwal",
        ["Hijri.Month.11"] = "Dhu al-Qi'dah",
        ["Hijri.Month.12"] = "Dhu al-Hijjah",
        ["Compass.N"] = "N",
        ["Compass.NE"] = "NE",
        ["Compass.E"] = "E",
        ["Compass.SE"] = "SE",
        ["Compass.S"] = "S",
        ["Compass.SW"] = "SW",
        ["Compass.W"] = "W",
        ["Compass.NW"] = "NW",
        ["AdhkarSubtitle"] = "Short offline texts — not a full library.",
        ["AdhkarDisclaimer"] = "Brief reminder texts. Consult trusted sources for full recitation. Latin digits 0-9.",
        ["Adhkar.AfterPrayer"] = "After prayer",
        ["Adhkar.AfterPrayer.1.Arabic"] = "أَسْتَغْفِرُ اللَّهَ (ثلاثًا)",
        ["Adhkar.AfterPrayer.1.Translation"] = "I seek forgiveness from Allah (three times)",
        ["Adhkar.AfterPrayer.2.Arabic"] = "اللَّهُمَّ أَنْتَ السَّلَامُ وَمِنْكَ السَّلَامُ، تَبَارَكْتَ يَا ذَا الْجَلَالِ وَالْإِكْرَامِ",
        ["Adhkar.AfterPrayer.2.Translation"] = "O Allah, You are Peace and from You is peace. Blessed are You, O Owner of majesty and honor.",
        ["Adhkar.AfterPrayer.3.Arabic"] = "سُبْحَانَ اللَّهِ · الْحَمْدُ لِلَّهِ · اللَّهُ أَكْبَرُ",
        ["Adhkar.AfterPrayer.3.Translation"] = "Glory be to Allah · Praise be to Allah · Allah is the Greatest",
        ["Adhkar.Morning"] = "Morning adhkar",
        ["Adhkar.Morning.1.Arabic"] = "أَصْبَحْنَا وَأَصْبَحَ الْمُلْكُ لِلَّهِ، وَالْحَمْدُ لِلَّهِ",
        ["Adhkar.Morning.1.Translation"] = "We have entered the morning and the dominion belongs to Allah, and all praise is for Allah.",
        ["Adhkar.Morning.2.Arabic"] = "بِسْمِ اللَّهِ الَّذِي لَا يَضُرُّ مَعَ اسْمِهِ شَيْءٌ فِي الْأَرْضِ وَلَا فِي السَّمَاءِ وَهُوَ السَّمِيعُ الْعَلِيمُ",
        ["Adhkar.Morning.2.Translation"] = "In the name of Allah with whose name nothing on earth or in the heavens can cause harm, and He is the All-Hearing, All-Knowing.",
        ["Adhkar.Evening"] = "Evening adhkar",
        ["Adhkar.Evening.1.Arabic"] = "أَمْسَيْنَا وَأَمْسَى الْمُلْكُ لِلَّهِ، وَالْحَمْدُ لِلَّهِ",
        ["Adhkar.Evening.1.Translation"] = "We have entered the evening and the dominion belongs to Allah, and all praise is for Allah.",
        ["Adhkar.Evening.2.Arabic"] = "أَعُوذُ بِكَلِمَاتِ اللَّهِ التَّامَّاتِ مِنْ شَرِّ مَا خَلَقَ",
        ["Adhkar.Evening.2.Translation"] = "I seek refuge in the perfect words of Allah from the evil of what He has created.",
        ["AppOptions"] = "App options",
        ["AutoStartEnabled"] = "Start with Windows",
        ["ShowMissedAlertOnResume"] = "Alert when a prayer notification was missed (resume / late start)",
        ["ToggleOn"] = "On",
        ["ToggleOff"] = "Off",
        ["RefreshProcessStatus"] = "Refresh process status",
        ["ProcessStatusFormat"] = "Working set: {0} MB · remaining planned events: {1} · auto-start: {2}",
        ["AutoStartRegisteredYes"] = "registered",
        ["AutoStartRegisteredNo"] = "not registered",
        ["MissedAlertTitle"] = "Missed prayer alert",
        ["MissedAlertBodyOne"] = "Missed {0} ({1}). Late adhan is not replayed.",
        ["MissedAlertBodyMany"] = "Missed {0} alerts: {1}. Late adhan is not replayed.",
        ["SoundLibrary"] = "Sound library",
        ["SoundLibraryHint"] = "Choose the prayer-start adhan and a pre-alert cue (takbir / hayya 'ala as-salah / soft tone). You can add your own files.",
        ["AdhanSound"] = "Adhan sound (prayer start)",
        ["PreAlertSound"] = "Pre-alert sound",
        ["PreviewSound"] = "Preview",
        ["AddSound"] = "Add sound…",
        ["StopSound"] = "Stop",
        ["SoundLibraryNote"] = "Adhan samples from PrayTimes.org for personal offline use. Takbir and Hayya cues are short synthetic tones you can replace. Re-verify licensing before any public release.",
        ["SoundAdded"] = "Sound added to the library.",
        ["SoundAddFailed"] = "Could not add sound:",
        ["PhaseBanner"] = "Phase 0–7: shell through polish & personal delivery",
    };
}
