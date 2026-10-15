using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Controls;

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

    private static readonly string[] _tones = ["attn", "fail", "ok"];

    private readonly TextBlock _word = new();

    static StateChip()
    {
        _ = ChipProperty.Changed.AddClassHandler<StateChip>((chip, _) => chip.Apply());
        _ = FallbackProperty.Changed.AddClassHandler<StateChip>((chip, _) => chip.Apply());
    }

    /// <summary>Initializes a new instance of the <see cref="StateChip"/> class, empty and hidden until it has a chip to draw.</summary>
    public StateChip()
    {
        Classes.Add("chip");
        Child = _word;
        IsVisible = false;
    }

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

    // The word as served, and the one class its tone picks; no chip at all draws nothing and takes no room.
    private void Apply()
    {
        var view = ChipView.Resolve(Chip, Fallback);
        foreach (var tone in _tones)
        {
            _ = Classes.Remove(tone);
        }

        IsVisible = view is not null;
        _word.Text = view?.Word;
        if (view?.ToneClass is { } toneClass)
        {
            Classes.Add(toneClass);
        }
    }
}
#pragma warning restore MA0182
