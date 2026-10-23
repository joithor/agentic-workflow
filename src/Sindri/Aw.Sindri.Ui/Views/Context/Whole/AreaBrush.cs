using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>A palette position (0 to 5) as the area colour's brush from Theme/Tokens.axaml: a view model carries the position, never a colour.</summary>
#pragma warning disable MA0182 // used from XAML (the area swatches)
internal sealed class AreaBrush : IValueConverter
{
    /// <summary>Gets the one converter.</summary>
    public static AreaBrush Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int colour && Application.Current is { } app
        && app.TryGetResource(string.Create(CultureInfo.InvariantCulture, $"Brush.Area{colour + 1}"), app.ActualThemeVariant, out var found) && found is IBrush brush
            ? brush
            : Brushes.Gray;

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
