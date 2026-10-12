using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// The shell's top bar: binds to the shell, and passes the health pill's click to it. It also fits itself to the window
/// (§3.2, minimum 900 px at text scale up to 1.3): with room the ⌘K field is 160–300 px wide; when the navigation, that
/// field and the pill don't fit, the field folds to its magnifier, and if that isn't enough the pill's age drops (the
/// pill's accessible name keeps it). The pill's word always shows. This is layout only; the shell decides what the words are.
/// </summary>
internal sealed partial class TopBar : UserControl
{
    // The ⌘K field's column with room: a 100* star column of 184–324 px (the field's 160–300 px and its 12 px margins).
    private const double SearchMin = 184;
    private const double SearchMax = 324;

    // Growing back takes this much more room than shrinking did, so a ticking age ("9s" to "10s") can't make the bar flicker.
    private const double Slack = 8;

    private Fit _fit = Fit.Full;
    private double _ageWidth;
    private double _compactSearch = 60;

    public TopBar()
    {
        InitializeComponent();
    }

    // How far the bar has stepped down.
    private enum Fit
    {
        Full,
        CompactSearch,
        NoAge,
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        Remember();
        var fit = Choose(availableSize.Width);
        if (fit != _fit)
        {
            Apply(fit);
            size = base.MeasureOverride(availableSize);
            Remember();
        }

        return size;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // A part can grow after the bar was measured (a text-size change reaches it late); measure again to step down.
        Remember();
        if (Choose(finalSize.Width) != _fit)
        {
            InvalidateMeasure();
        }

        return base.ArrangeOverride(finalSize);
    }

    // The widest step that fits: the bar's padding, the brand and the navigation are fixed; then the ⌘K field (full or
    // folded) and the pill with its age (measured while shown, remembered while dropped).
    private Fit Choose(double width)
    {
        if (double.IsInfinity(width))
        {
            return Fit.Full;
        }

        var age = PillAgeText.Text is { Length: > 0 } ? _ageWidth + PillGroupSpacing : 0;
        var pillWithAge = PillGroup.DesiredSize.Width + (PillAge.IsVisible ? 0 : age);
        var padding = Content is Border bar ? bar.Padding.Left + bar.Padding.Right : 0;
        var fixedWidth = padding + Brand.DesiredSize.Width + NavSegments.DesiredSize.Width;
        bool Fits(double search, double pill, Fit step) => fixedWidth + search + pill <= width - (step < _fit ? Slack : 0);

        return Fits(SearchMin, pillWithAge, Fit.Full) ? Fit.Full
            : Fits(_compactSearch, pillWithAge, Fit.CompactSearch) ? Fit.CompactSearch
            : Fit.NoAge;
    }

    private double PillGroupSpacing => PillGroup.Spacing;

    // The ⌘K field's column (the fifth).
    private ColumnDefinition SearchColumn => BarGrid.ColumnDefinitions[4];

    private void Apply(Fit fit)
    {
        _fit = fit;
        var compact = fit != Fit.Full;
        SearchField.Classes.Set("compact", compact);
        SearchColumn.Width = compact ? GridLength.Auto : new GridLength(100, GridUnitType.Star);
        SearchColumn.MinWidth = compact ? 0 : SearchMin;
        SearchColumn.MaxWidth = compact ? double.PositiveInfinity : SearchMax;
        PillAge.IsVisible = fit != Fit.NoAge;
    }

    // The widths a dropped part would need again.
    private void Remember()
    {
        if (PillAge.IsVisible && PillAge.DesiredSize.Width > 0)
        {
            _ageWidth = PillAge.DesiredSize.Width;
        }

        if (_fit != Fit.Full && SearchField.DesiredSize.Width > 0)
        {
            _compactSearch = SearchField.DesiredSize.Width;
        }
    }

    private void OnHealthPill(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.GoTo(PageId.Health);

    // Refresh re-reads the status (a query); the pill shows the answer when it comes.
    private void OnRefresh(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell)
        {
            _ = shell.Health.Refresh(CancellationToken.None);
        }
    }
}
