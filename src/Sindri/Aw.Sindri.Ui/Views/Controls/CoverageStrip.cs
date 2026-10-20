using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until slice 2 places the control in the context page

/// <summary>The coverage strip of the context map's control row (F3 spec 1 §2, "1d"): one bar per area and a sentence, as one button.</summary>
internal sealed class CoverageStrip : Button
{
    /// <summary>The areas, one bar each.</summary>
    internal static readonly StyledProperty<IReadOnlyList<CoverageArea>> AreasProperty =
        AvaloniaProperty.Register<CoverageStrip, IReadOnlyList<CoverageArea>>(nameof(Areas), []);

    /// <summary>The sentence's bold lead.</summary>
    internal static readonly StyledProperty<string> LeadProperty = AvaloniaProperty.Register<CoverageStrip, string>(nameof(Lead), "");

    /// <summary>The rest of the sentence.</summary>
    internal static readonly StyledProperty<string> RestProperty = AvaloniaProperty.Register<CoverageStrip, string>(nameof(Rest), "");

    private readonly StackPanel _rows = new();

    /// <summary>Gets or sets the areas.</summary>
    internal IReadOnlyList<CoverageArea> Areas
    {
        get => GetValue(AreasProperty);
        set => SetValue(AreasProperty, value);
    }

    /// <summary>Gets or sets the sentence's lead.</summary>
    internal string Lead
    {
        get => GetValue(LeadProperty);
        set => SetValue(LeadProperty, value);
    }

    /// <summary>Gets or sets the rest of the sentence.</summary>
    internal string Rest
    {
        get => GetValue(RestProperty);
        set => SetValue(RestProperty, value);
    }

    /// <summary>The panel the flyout shows (its rows), for tests.</summary>
    internal Panel FlyoutRows => _rows;
}

#pragma warning restore MA0182
