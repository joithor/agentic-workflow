using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until slice 2 places the control in the context page

/// <summary>
/// The context map's pannable, zoomable stage (F3 spec 1 §2): it holds the world as its child and moves it with a matrix.
/// A drag on empty canvas pans, the wheel pans, Ctrl or Cmd with the wheel zooms around the cursor, and the scale stays
/// between 62 % and 100 %. The step and fit moves animate over Motion.Base, which is zero under reduced motion. A 22 px dotted
/// grid fills the stage behind the world and moves with it.
/// </summary>
internal sealed class ZoomBorder : Border
{
    /// <summary>The view: scale and offset of the world.</summary>
    internal static readonly DirectProperty<ZoomBorder, ZoomView> ViewProperty =
        AvaloniaProperty.RegisterDirect<ZoomBorder, ZoomView>(nameof(View), z => z.View);

    /// <summary>The world's size, which <see cref="Fit"/> fits to the stage.</summary>
    internal static readonly StyledProperty<Size> WorldSizeProperty = AvaloniaProperty.Register<ZoomBorder, Size>(nameof(WorldSize), new Size(1360, 872));

    private const double WheelStep = 40;
    private const double ZoomStep = 1.2;
    private const double ClickSlop = 3;
    private const double DotPitch = 22;

    private (ZoomView From, ZoomView To, TimeSpan? Start, TimeSpan Length)? _move;
    private (Point Pointer, ZoomView View, bool Moved)? _drag;
    private DrawingBrush? _dots;

    static ZoomBorder()
    {
        _ = WorldSizeProperty.Changed.AddClassHandler<ZoomBorder>((z, _) => z.Keep());
    }

    /// <summary>Initializes a new instance of the <see cref="ZoomBorder"/> class.</summary>
    public ZoomBorder()
    {
        ClipToBounds = true;
        Focusable = false;
        Background = Brushes.Transparent;
    }

    /// <summary>Raised when the pointer is pressed and released on empty canvas without moving (a click, which closes the inspector).</summary>
    internal event EventHandler? EmptyClicked;

    /// <summary>Gets the current view.</summary>
    internal ZoomView View { get; private set; } = ZoomView.Identity;

    /// <summary>Gets or sets the world's size.</summary>
    internal Size WorldSize
    {
        get => GetValue(WorldSizeProperty);
        set => SetValue(WorldSizeProperty, value);
    }

    /// <summary>Gets a value indicating whether a zoom transition is running.</summary>
    internal bool IsMoving => _move is not null;

    /// <summary>Zooms in one step around the middle of the stage.</summary>
    internal void ZoomIn() => MoveTo(View.ZoomAt(Middle, ZoomStep));

    /// <summary>Zooms out one step around the middle of the stage.</summary>
    internal void ZoomOut() => MoveTo(View.ZoomAt(Middle, 1 / ZoomStep));

    /// <summary>Shows all of the world (clamped at the 62 % floor).</summary>
    internal void Fit() => MoveTo(ZoomView.Fit(Bounds.Size, WorldSize));

    /// <summary>Pans the least that brings a world rectangle fully into view, clear of the inspector over the right edge.</summary>
    /// <param name="target">The rectangle, in world coordinates.</param>
    /// <param name="rightInset">Width of the panel over the stage's right edge.</param>
    internal void Reveal(Rect target, double rightInset = 0) => MoveTo(View.Reveal(target, Bounds.Size, rightInset));

    /// <summary>Puts the view where an unfinished transition was going, now.</summary>
    internal void Settle()
    {
        if (_move is { } move)
        {
            _move = null;
            Apply(move.To);
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _dots = null;   // the brushes it reads come from the tree it is now in
        Apply(View);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Settle();
        _drag = (e.GetPosition(this), View, false);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is { } drag)
        {
            var delta = e.GetPosition(this) - drag.Pointer;
            _drag = drag with { Moved = drag.Moved || Math.Abs(delta.X) + Math.Abs(delta.Y) > ClickSlop };
            Apply(drag.View.PanBy(delta.X, delta.Y).Kept(Bounds.Size, WorldSize));
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is { } drag)
        {
            _drag = null;
            e.Pointer.Capture(control: null);
            if (!drag.Moved)
            {
                EmptyClicked?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Settle();
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != KeyModifiers.None)
        {
            Apply(View.ZoomAt(e.GetPosition(this), Math.Pow(1.1, e.Delta.Y)).Kept(Bounds.Size, WorldSize));
        }
        else
        {
            Apply(View.PanBy(-e.Delta.X * WheelStep, e.Delta.Y * WheelStep).Kept(Bounds.Size, WorldSize));
        }

        e.Handled = true;
    }

    private Point Middle => new(Bounds.Width / 2, Bounds.Height / 2);

    private void Keep() => Apply(View.Kept(Bounds.Size, WorldSize));

    // The move to a view: at once under reduced motion or with nothing to draw frames on, else over Motion.Base.
    private void MoveTo(ZoomView target)
    {
        Settle();
        var length = this.TryFindResource("Motion.Base", ActualThemeVariant, out var value) && value is TimeSpan span ? span : TimeSpan.Zero;
        if (length <= TimeSpan.Zero || TopLevel.GetTopLevel(this) is not { } top || target == View)
        {
            Apply(target);
            return;
        }

        _move = (View, target, null, length);
        top.RequestAnimationFrame(Frame);
    }

    private void Frame(TimeSpan now)
    {
        if (_move is not { } move)
        {
            return;
        }

        var start = move.Start ?? now;
        var t = Math.Clamp((now - start) / move.Length, 0, 1);
        if (t >= 1)
        {
            Settle();
            return;
        }

        _move = move with { Start = start };
        var eased = 1 - Math.Pow(1 - t, 3);
        Apply(ZoomView.Lerp(move.From, move.To, eased));
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Frame);
    }

    private void Apply(ZoomView view)
    {
        var changed = view != View;
        var old = View;
        View = view;
        if (Child is { } world)
        {
            world.RenderTransformOrigin = RelativePoint.TopLeft;
            world.RenderTransform = new ImmutableTransform(new Matrix(view.Scale, 0, 0, view.Scale, view.X, view.Y));
        }

        _dots ??= Dots();
        _dots.Transform = new ImmutableTransform(new Matrix(view.Scale, 0, 0, view.Scale, view.X, view.Y));
        Background = _dots;
        if (changed)
        {
            RaisePropertyChanged(ViewProperty, old, view);
        }
    }

    // The 22 px dot grid in HairlineStrong, in a tile that scales and moves with the view, over a transparent base so the pointer still lands here.
    private DrawingBrush Dots()
    {
        var dot = this.TryFindResource("Brush.HairlineStrong", ActualThemeVariant, out var found) && found is IBrush brush ? brush : Brushes.Gray;
        var tile = new Rect(0, 0, DotPitch, DotPitch);
        var drawing = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(tile) },
                new GeometryDrawing { Brush = dot, Geometry = new EllipseGeometry(new Rect((DotPitch / 2) - 1, (DotPitch / 2) - 1, 2, 2)) },
            },
        };
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            SourceRect = new RelativeRect(tile, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(tile, RelativeUnit.Absolute),
        };
    }
}

#pragma warning restore MA0182
