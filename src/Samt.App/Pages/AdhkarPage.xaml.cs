using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Samt_App.ViewModels;

namespace Samt_App.Pages;

public sealed partial class AdhkarPage : Page
{
    public AdhkarPage()
    {
        ViewModel = new AdhkarViewModel(App.Localization);
        InitializeComponent();
        App.Localization.LanguageChanged += (_, _) => ApplyLanguageUi();
        Loaded += (_, _) => ApplyLanguageUi();
    }

    public AdhkarViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyLanguageUi();
    }

    private void ApplyLanguageUi()
    {
        FlowDirection = App.Localization.FlowDirection;
        TitleText.Text = App.Localization.Get("NavAdhkar");
        SubtitleText.Text = App.Localization.Get("AdhkarSubtitle");
        ViewModel.RefreshLabels();
        Bindings.Update();
    }
}
