using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Samt_App.Pages;

/// <summary>
/// PROTOTYPE — overlay look variants. Not production. Delete after Phase 4 decision.
/// </summary>
public sealed partial class DesignLabPage : Page
{
    public DesignLabPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            FlowDirection = App.Localization.FlowDirection;
            LabLabel.Text = App.Localization.Get("NavDesignLab");
            ShowVariant("A");
        };
    }

    private void VariantButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            ShowVariant(tag);
        }
    }

    private void ShowVariant(string tag)
    {
        VariantA.Visibility = tag == "A" ? Visibility.Visible : Visibility.Collapsed;
        VariantB.Visibility = tag == "B" ? Visibility.Visible : Visibility.Collapsed;
        VariantC.Visibility = tag == "C" ? Visibility.Visible : Visibility.Collapsed;
    }
}
