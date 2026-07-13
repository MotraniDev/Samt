using Microsoft.UI.Xaml;

namespace Samt_App.Services;

public enum AppThemeChoice
{
    System = 0,
    Light = 1,
    Dark = 2
}

public sealed class ThemeService
{
    public AppThemeChoice Current { get; private set; } = AppThemeChoice.System;

    public void Apply(Window window, AppThemeChoice choice)
    {
        Current = choice;
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = choice switch
            {
                AppThemeChoice.Light => ElementTheme.Light,
                AppThemeChoice.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }
}
