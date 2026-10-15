using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until the U8 pages place the chip
/// <summary>
/// The state chip: a small bordered word, coloured by its tone. The word is drawn as served; the tone picks the class
/// (attn, fail, ok, or none for neutral). The served chip wins; a chip the client draws itself is the fallback.
/// </summary>
internal sealed class StateChip : Border
{
    /// <summary>The chip the API served.</summary>
    public static readonly StyledProperty<Api.Workflow.StateChip?> ChipProperty = AvaloniaProperty.Register<StateChip, Api.Workflow.StateChip?>(nameof(Chip));

    /// <summary>The chip a view draws itself when none is served.</summary>
    public static readonly StyledProperty<Api.Workflow.StateChip?> FallbackProperty = AvaloniaProperty.Register<StateChip, Api.Workflow.StateChip?>(nameof(Fallback));

    /// <summary>Gets or sets the served chip.</summary>
    public Api.Workflow.StateChip? Chip
    {
        get => GetValue(ChipProperty);
        set => SetValue(ChipProperty, value);
    }

    /// <summary>Gets or sets the chip a view draws itself.</summary>
    public Api.Workflow.StateChip? Fallback
    {
        get => GetValue(FallbackProperty);
        set => SetValue(FallbackProperty, value);
    }
}
#pragma warning restore MA0182
