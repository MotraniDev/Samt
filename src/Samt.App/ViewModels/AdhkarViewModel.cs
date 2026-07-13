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

    public ObservableCollection<AdhkarSectionVm> Sections { get; } = [];

    public string Disclaimer => _localization.Get("AdhkarDisclaimer");
    public string Subtitle => _localization.Get("AdhkarSubtitle");

    public void RefreshLabels()
    {
        Rebuild();
        OnPropertyChanged(nameof(Disclaimer));
        OnPropertyChanged(nameof(Subtitle));
    }

    public void OpenReader(AdhkarCollectionKind kind)
        => App.AdhkarReminders?.OpenReader(kind);

    private void Rebuild()
    {
        Sections.Clear();
        foreach (var collection in AdhkarCatalog.All)
        {
            var items = collection.Items.Select(i =>
            {
                var translation = i.TranslationKey is null
                    ? string.Empty
                    : _localization.Get(i.TranslationKey);
                var repeat = i.RepeatCount is { } n and > 1
                    ? LatinDigits.EnsureLatin($"× {n}")
                    : null;
                return new AdhkarItemVm(i.ArabicText, translation, repeat);
            }).ToList();

            Sections.Add(new AdhkarSectionVm(
                collection.Kind,
                _localization.Get(collection.TitleKey),
                items));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record AdhkarSectionVm(
    AdhkarCollectionKind Kind,
    string Title,
    IReadOnlyList<AdhkarItemVm> Items);

public sealed record AdhkarItemVm(string ArabicText, string Translation, string? RepeatBadge);
