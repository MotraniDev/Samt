using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

    public ObservableCollection<AdhkarSection> Sections { get; } = [];

    public string Disclaimer => _localization.Get("AdhkarDisclaimer");

    public void RefreshLabels()
    {
        Rebuild();
        OnPropertyChanged(nameof(Disclaimer));
    }

    private void Rebuild()
    {
        Sections.Clear();
        Sections.Add(BuildSection("Adhkar.AfterPrayer", "Adhkar.AfterPrayer", itemCount: 3));
        Sections.Add(BuildSection("Adhkar.Morning", "Adhkar.Morning", itemCount: 2));
        Sections.Add(BuildSection("Adhkar.Evening", "Adhkar.Evening", itemCount: 2));
    }

    private AdhkarSection BuildSection(string titleKey, string itemPrefix, int itemCount)
    {
        var items = new List<AdhkarItem>(itemCount);
        for (var i = 1; i <= itemCount; i++)
        {
            items.Add(new AdhkarItem(
                _localization.Get($"{itemPrefix}.{i}.Arabic"),
                _localization.Get($"{itemPrefix}.{i}.Translation")));
        }

        return new AdhkarSection(_localization.Get(titleKey), items);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record AdhkarSection(string Title, IReadOnlyList<AdhkarItem> Items);

public sealed record AdhkarItem(string ArabicText, string Translation);
