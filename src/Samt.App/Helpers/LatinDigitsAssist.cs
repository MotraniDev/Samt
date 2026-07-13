using Microsoft.UI.Xaml;
using Samt.Core.Formatting;

namespace Samt_App.Helpers;

/// <summary>
/// Attached property that forces Latin (Western) digits on a control by setting
/// <see cref="FrameworkElement.Language"/> to <see cref="LatinDigits.XamlLanguageTag"/>.
/// FlowDirection is left unchanged so RTL Arabic UI still works.
/// </summary>
public static class LatinDigitsAssist
{
    public static readonly DependencyProperty ForceProperty =
        DependencyProperty.RegisterAttached(
            "Force",
            typeof(bool),
            typeof(LatinDigitsAssist),
            new PropertyMetadata(false, OnForceChanged));

    public static void SetForce(DependencyObject element, bool value)
        => element.SetValue(ForceProperty, value);

    public static bool GetForce(DependencyObject element)
        => (bool)element.GetValue(ForceProperty);

    private static void OnForceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe)
        {
            return;
        }

        if (e.NewValue is true)
        {
            fe.Language = LatinDigits.XamlLanguageTag;
        }
    }

    /// <summary>Apply Latin language tag to any framework element that shows numbers.</summary>
    public static void Apply(FrameworkElement element)
        => element.Language = LatinDigits.XamlLanguageTag;
}
