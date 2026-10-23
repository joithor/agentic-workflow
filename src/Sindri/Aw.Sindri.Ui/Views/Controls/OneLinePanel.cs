using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// A row that never wraps: the children that fit the width are laid out in one line, in order, and the others are hidden. The count that fit
/// is <see cref="VisibleCount"/>, announced by <see cref="VisibleCountChangedEvent"/>, so the owner can offer the rest ("+N more").
/// </summary>
internal sealed class OneLinePanel : Panel
{
    /// <summary>Raised (bubbling) when the number of children that fit the row, or the number of children, changed.</summary>
    internal static readonly RoutedEvent<RoutedEventArgs> VisibleCountChangedEvent =
        RoutedEvent.Register<OneLinePanel, RoutedEventArgs>(nameof(VisibleCountChanged), RoutingStrategies.Bubble);

    private const double Gap = 6;

    // A hidden child has no desired size, so the width it had when it was last shown is kept to decide whether it fits again.
    private readonly Dictionary<Control, double> _widths = [];
    private int _total;

    /// <summary>Gets how many children are laid out in the row.</summary>
    internal int VisibleCount { get; private set; }

    /// <summary>Raised when <see cref="VisibleCount"/> changed.</summary>
    internal event EventHandler<RoutedEventArgs> VisibleCountChanged
    {
        add => AddHandler(VisibleCountChangedEvent, value);
        remove => RemoveHandler(VisibleCountChangedEvent, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children.ToList();
        foreach (var child in children.Where(c => c.IsVisible))
        {
            child.Measure(Size.Infinity);
            _widths[child] = child.DesiredSize.Width;
        }

        var fit = 0;
        var used = 0.0;
        foreach (var child in children)
        {
            var next = used + (fit > 0 ? Gap : 0) + _widths.GetValueOrDefault(child);
            if (fit > 0 && next > availableSize.Width)
            {
                break;
            }

            (fit, used) = (fit + 1, next);
        }

        for (var i = 0; i < children.Count; i++)
        {
            children[i].IsVisible = i < fit;
        }

        var height = children.Where(c => c.IsVisible).Select(c => c.DesiredSize.Height).DefaultIfEmpty().Max();
        Announce(fit, children.Count);
        return new Size(Math.Min(used, double.IsInfinity(availableSize.Width) ? used : availableSize.Width), height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width + Gap;
        }

        return finalSize;
    }

    private void Announce(int fit, int total)
    {
        if (fit != VisibleCount || total != _total)
        {
            (VisibleCount, _total) = (fit, total);
            RaiseEvent(new RoutedEventArgs(VisibleCountChangedEvent));
        }
    }
}
