using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views;

/// <summary>A state glyph or a mark drawn as a vector icon (stub).</summary>
internal sealed class StateIcon : Control
{
    public static readonly StyledProperty<StateGlyph> GlyphProperty = AvaloniaProperty.Register<StateIcon, StateGlyph>(nameof(Glyph));

    public static readonly StyledProperty<string?> IconProperty = AvaloniaProperty.Register<StateIcon, string?>(nameof(Icon));

    public StateGlyph Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<Geometry?> GeometryProperty = AvaloniaProperty.Register<StateIcon, Geometry?>(nameof(Geometry));

    public Geometry? Geometry => GetValue(GeometryProperty);
}
