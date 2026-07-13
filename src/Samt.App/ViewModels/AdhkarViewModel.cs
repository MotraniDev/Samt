using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Samt.Core.Adhkar;
using Samt.Core.Formatting;
using Samt_App.Services;

namespace Samt_App.ViewModels;

public sealed class AdhkarViewModel : INotifyPropertyChanged
{
    private readonly LocalizationService _localization;

    public AdhkarViewModel(LocalizationService localization)
    {
        _localization = localization;
        Rebuild();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AdhkarGroupVm> Groups { get; } = [];

    public string Disclaimer => _localization.Get("AdhkarDisclaimer");
    public string Subtitle => _localization.Get("AdhkarSubtitle");
    public string SourceLine => _localization.Get("Adhkar.Source.AzkarMe");

    public void RefreshLabels()
    {
        Rebuild();
        OnPropertyChanged(nameof(Disclaimer));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(SourceLine));
    }

    public void OpenReader(AdhkarCollectionKind kind)
        => App.AdhkarReminders?.OpenReader(kind);

    private void Rebuild()
    {
        Groups.Clear();
        var openLabel = _localization.Get("AdhkarOpenReader");

        foreach (var group in Enum.GetValues<AdhkarLibraryGroup>())
        {
            var sections = AdhkarCatalog.ByGroup(group)
                .Select(c => new AdhkarSectionVm(
                    c.Kind,
                    _localization.Get(c.TitleKey),
                    c.IconHint,
                    LatinDigits.EnsureLatin(
                        string.Format(_localization.Get("AdhkarItemCountFormat"), c.Items.Count)),
                    openLabel))
                .ToList();

            if (sections.Count == 0)
            {
                continue;
            }

            Groups.Add(new AdhkarGroupVm(
                _localization.Get(GroupTitleKey(group)),
                sections));
        }
    }

    private static string GroupTitleKey(AdhkarLibraryGroup group) => group switch
    {
        AdhkarLibraryGroup.Daily => "Adhkar.Group.Daily",
        AdhkarLibraryGroup.PrayerRelated => "Adhkar.Group.Prayer",
        AdhkarLibraryGroup.LifeSituations => "Adhkar.Group.Life",
        AdhkarLibraryGroup.PraiseAndDuas => "Adhkar.Group.Praise",
        _ => "NavAdhkar"
    };

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record AdhkarGroupVm(string Title, IReadOnlyList<AdhkarSectionVm> Sections);

public sealed record AdhkarSectionVm(
    AdhkarCollectionKind Kind,
    string Title,
    string Icon,
    string CountLabel,
    string OpenLabel);
