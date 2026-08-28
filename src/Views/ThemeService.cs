using System.Windows;
using CleaN.Core;
using CleaN.ViewModels;

namespace CleaN.Views;

/// <summary>
/// Swaps the palette dictionary in place. Every style uses DynamicResource, so the whole
/// window restyles itself the moment the dictionary is replaced.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const int PaletteIndex = 0;

    public void Apply(AppTheme theme)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var source = theme == AppTheme.Dark ? "Assets/Themes/Dark.xaml" : "Assets/Themes/Light.xaml";
        var palette = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };

        var dictionaries = application.Resources.MergedDictionaries;
        if (dictionaries.Count > PaletteIndex)
        {
            dictionaries[PaletteIndex] = palette;
        }
        else
        {
            dictionaries.Insert(PaletteIndex, palette);
        }
    }
}
