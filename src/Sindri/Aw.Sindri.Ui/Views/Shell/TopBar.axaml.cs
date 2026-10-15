using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// The shell's top bar: binds to the shell, and passes the health pill's click to it. It also fits itself to the window
/// (§3.2, minimum 900 px at text scale up to 1.3): with room the ⌘K field is 160–300 px wide; when the navigation, that
/// field and the pill don't fit, the field folds to its magnifier; if that isn't enough the pill's age drops (the pill's
/// accessible name keeps it), and then the brand keeps its mark alone (the window and the bar's region are still named
/// Sindri). The pill's word always shows. This is layout only; the shell decides what the words are.
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
    private double _brandWordWidth;
    private double _compactSearch = 60;

    // Refresh had focus when it was disabled (in flight) or hidden (recovered); see KeepRefreshFocus.
    private bool _refreshHeldFocus;
    private NavigationMethod _refreshMethod = NavigationMethod.Tab;

    public TopBar()
    {
        InitializeComponent();
        RefreshButton.GotFocus += OnRefreshGotFocus;
        RefreshButton.LostFocus += OnRefreshLostFocus;
        RefreshButton.PropertyChanged += OnRefreshChanged;
    }

    // How far the bar has stepped down.
    private enum Fit
    {
        Full,
        CompactSearch,
        NoAge,
        NoBrandWord,
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

        var age = PillAgeText.Text is { Length: > 0 } ? _ageWidth + PillGroup.Spacing : 0;
        var pill = PillGroup.DesiredSize.Width - (PillAge.IsVisible ? age : 0);
        var word = _brandWordWidth + Brand.Spacing;
        var padding = Content is Border bar ? bar.Padding.Left + bar.Padding.Right : 0;
        var fixedWidth = padding + Brand.DesiredSize.Width - (BrandWord.IsVisible ? word : 0) + NavSegments.DesiredSize.Width;
        bool Fits(double brand, double search, double pillAge, Fit step) => fixedWidth + brand + search + pill + pillAge <= width - (step < _fit ? Slack : 0);

        return Fits(word, SearchMin, age, Fit.Full) ? Fit.Full
            : Fits(word, _compactSearch, age, Fit.CompactSearch) ? Fit.CompactSearch
            : Fits(word, _compactSearch, 0, Fit.NoAge) ? Fit.NoAge
            : Fit.NoBrandWord;
    }

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
        PillAge.IsVisible = fit < Fit.NoAge;
        BrandWord.IsVisible = fit < Fit.NoBrandWord;
    }

    // The widths a dropped part would need again.
    private void Remember()
    {
        if (PillAge.IsVisible && PillAge.DesiredSize.Width > 0)
        {
            _ageWidth = PillAge.DesiredSize.Width;
        }

        if (BrandWord.IsVisible && BrandWord.DesiredSize.Width > 0)
        {
            _brandWordWidth = BrandWord.DesiredSize.Width;
        }

        if (_fit != Fit.Full && SearchField.DesiredSize.Width > 0)
        {
            _compactSearch = SearchField.DesiredSize.Width;
        }
    }

    private void OnRefreshGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _refreshHeldFocus = true;
        _refreshMethod = e.NavigationMethod;
    }

    // Focus that leaves a usable Refresh went somewhere on purpose; focus taken by disabling or hiding it comes back.
    private void OnRefreshLostFocus(object? sender, FocusChangedEventArgs e) =>
        _refreshHeldFocus &= !(RefreshButton.IsEffectivelyVisible && RefreshButton.IsEffectivelyEnabled);

    private void OnRefreshChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_refreshHeldFocus && (e.Property == IsVisibleProperty || e.Property == IsEnabledProperty))
        {
            Dispatcher.UIThread.Post(KeepRefreshFocus, DispatcherPriority.Loaded);
        }
    }

    // §4.1, §11.2: a refresh that fails leaves focus on Refresh (it was only disabled while in flight); one that
    // recovers hides Refresh, and focus moves to its neighbour, the pill. Focus the user moved elsewhere stays there.
    private void KeepRefreshFocus()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        if (!_refreshHeldFocus || (focused is { IsEffectivelyVisible: true } && focused != RefreshButton))
        {
            _refreshHeldFocus = false;
            return;
        }

        if (!RefreshButton.IsEffectivelyVisible)
        {
            _refreshHeldFocus = false;
            _ = HealthPill.Focus(_refreshMethod);
        }
        else if (RefreshButton.IsEffectivelyEnabled && focused != RefreshButton)
        {
            _ = RefreshButton.Focus(_refreshMethod);
        }
    }

    // The pill opens Health, except while Sindri isn't running: the pill is disabled then (the Not running card is the
    // health state), and a click that still arrives opens nothing and remembers nothing.
    private void OnHealthPill(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel { IsNavigationEnabled: true } shell)
        {
            shell.GoTo(PageId.System);
        }
    }

    // Refresh re-reads the status (a query); the pill shows the answer when it comes.
    private void OnRefresh(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell)
        {
            _ = shell.Health.Refresh(CancellationToken.None);
        }
    }
}
