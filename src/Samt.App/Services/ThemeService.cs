using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Samt_App.Services;

/// <summary>
/// Applies curated theme packages. System follows OS light/dark using Light/Dark tokens.
/// Regional packages (Ramadan, Algeria, Morocco) are fixed visual identities.
/// </summary>
public sealed class ThemeService
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";
    public const string Ramadan = "ramadan";
    public const string Algeria = "algeria";
    public const string Morocco = "morocco";

    public string CurrentPackageId { get; private set; } = System;

    /// <summary>True when the effective surface is dark (for logo / contrast choices).</summary>
    public bool IsEffectivelyDark { get; private set; } = true;

    public void ApplyPackage(Window window, string? packageId)
    {
        CurrentPackageId = NormalizePackageId(packageId);
        if (window.Content is not FrameworkElement root)
        {
            return;
        }

        var (elementTheme, effectivelyDark) = ResolveTheme(CurrentPackageId, root);
        IsEffectivelyDark = effectivelyDark;
        root.RequestedTheme = elementTheme;
        ApplyPackageBrushes(root, CurrentPackageId, effectivelyDark);
    }

    /// <summary>Legacy helper used by older call sites.</summary>
    public void Apply(Window window, AppThemeChoice choice)
    {
        var id = choice switch
        {
            AppThemeChoice.Light => Light,
            AppThemeChoice.Dark => Dark,
            _ => System
        };
        ApplyPackage(window, id);
    }

    public static string NormalizePackageId(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return System;
        }

        return theme.Trim().ToLowerInvariant() switch
        {
            "light" => Light,
            "dark" => Dark,
            "ramadan" => Ramadan,
            "algeria" => Algeria,
            "morocco" => Morocco,
            "system" => System,
            _ => System
        };
    }

    private static (ElementTheme theme, bool effectivelyDark) ResolveTheme(string packageId, FrameworkElement root)
    {
        return packageId switch
        {
            Light => (ElementTheme.Light, false),
            Dark => (ElementTheme.Dark, true),
            // Ramadan: deep night field with gold — treat as dark
            Ramadan => (ElementTheme.Dark, true),
            // Algeria: deep green-navy — dark
            Algeria => (ElementTheme.Dark, true),
            // Morocco: warm terracotta/deep — dark base for gold readability
            Morocco => (ElementTheme.Dark, true),
            _ => root.ActualTheme == ElementTheme.Light
                ? (ElementTheme.Default, false)
                : (ElementTheme.Default, root.ActualTheme != ElementTheme.Light)
        };
    }

    private static void ApplyPackageBrushes(FrameworkElement root, string packageId, bool dark)
    {
        // Overlay accent tokens on the root so pages pick them up without full ResourceDictionary swap.
        // Keys mirror SamtTheme.xaml; missing keys fall through to app resources.
        var (gold, navy, green, surface) = packageId switch
        {
            Light => (
                ColorFrom("#8B6914"),
                ColorFrom("#E8EEF5"),
                ColorFrom("#2A6B50"),
                ColorFrom("#F7F4EC")),
            Ramadan => (
                ColorFrom("#E5C276"),
                ColorFrom("#1A0F24"),
                ColorFrom("#3D2A1F"),
                ColorFrom("#241833")),
            Algeria => (
                ColorFrom("#D4AF37"),
                ColorFrom("#0A1F14"),
                ColorFrom("#006233"),
                ColorFrom("#0F2A1C")),
            Morocco => (
                ColorFrom("#C9A227"),
                ColorFrom("#1A0A0A"),
                ColorFrom("#8B2500"),
                ColorFrom("#2A1210")),
            Dark => (
                ColorFrom("#C4A35A"),
                ColorFrom("#0B1F33"),
                ColorFrom("#1F4D3A"),
                ColorFrom("#0D2135")),
            _ when dark => (
                ColorFrom("#C4A35A"),
                ColorFrom("#0B1F33"),
                ColorFrom("#1F4D3A"),
                ColorFrom("#0D2135")),
            _ => (
                ColorFrom("#8B6914"),
                ColorFrom("#E8EEF5"),
                ColorFrom("#2A6B50"),
                ColorFrom("#F7F4EC"))
        };

        SetBrush(root, "SamtGoldBrush", gold);
        SetBrush(root, "SamtGoldSoftBrush", Lighten(gold, 0.15));
        SetBrush(root, "SamtGoldBrightBrush", Lighten(gold, 0.25));
        SetBrush(root, "SamtNavyBrush", navy);
        SetBrush(root, "SamtNavyDeepBrush", Darken(navy, 0.12));
        SetBrush(root, "SamtNavyMidBrush", Lighten(navy, 0.12));
        SetBrush(root, "SamtSurfaceBrush", surface);
        SetBrush(root, "SamtGreenBrush", green);
        SetBrush(root, "SamtAccentBrush", gold);
    }

    private static void SetBrush(FrameworkElement root, string key, Windows.UI.Color color)
    {
        root.Resources[key] = new SolidColorBrush(color);
    }

    private static Windows.UI.Color ColorFrom(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToByte(hex[..2], 16);
        var g = Convert.ToByte(hex[2..4], 16);
        var b = Convert.ToByte(hex[4..6], 16);
        return Windows.UI.Color.FromArgb(255, r, g, b);
    }

    private static Windows.UI.Color Lighten(Windows.UI.Color c, double amount)
    {
        byte Lift(byte v) => (byte)Math.Clamp(v + (int)((255 - v) * amount), 0, 255);
        return Windows.UI.Color.FromArgb(c.A, Lift(c.R), Lift(c.G), Lift(c.B));
    }

    private static Windows.UI.Color Darken(Windows.UI.Color c, double amount)
    {
        byte Drop(byte v) => (byte)Math.Clamp(v * (1 - amount), 0, 255);
        return Windows.UI.Color.FromArgb(c.A, Drop(c.R), Drop(c.G), Drop(c.B));
    }
}

/// <summary>Legacy enum kept for any remaining call sites; prefer package ids.</summary>
public enum AppThemeChoice
{
    System = 0,
    Light = 1,
    Dark = 2
}
