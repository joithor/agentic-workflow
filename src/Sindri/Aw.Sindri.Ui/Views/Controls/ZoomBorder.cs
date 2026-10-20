using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until slice 2 places the control in the context page

/// <summary>The context map's pannable, zoomable stage (F3 spec 1 §2): the world is its child and a matrix moves it.</summary>
internal sealed class ZoomBorder : Border
{
    /// <summary>The view: scale and offset of the world.</summary>
    internal static readonly DirectProperty<ZoomBorder, ZoomView> ViewProperty =
        AvaloniaProperty.RegisterDirect<ZoomBorder, ZoomView>(nameof(View), z => z.View);

    /// <summary>The world's size, which <see cref="Fit"/> fits to the stage.</summary>
    internal static readonly StyledProperty<Size> WorldSizeProperty = AvaloniaProperty.Register<ZoomBorder, Size>(nameof(WorldSize), new Size(1360, 872));

    /// <summary>Raised when the pointer is pressed and released on empty canvas without moving.</summary>
    internal event EventHandler? EmptyClicked;

    /// <summary>Gets the current view.</summary>
    internal ZoomView View { get; } = ZoomView.Identity;

    /// <summary>Gets or sets the world's size.</summary>
    internal Size WorldSize
    {
        get => GetValue(WorldSizeProperty);
        set => SetValue(WorldSizeProperty, value);
    }

    /// <summary>Gets a value indicating whether a zoom transition is running.</summary>
    internal bool IsMoving => View.Scale < 0;

    /// <summary>Zooms in one step around the middle of the stage.</summary>
    internal void ZoomIn() => throw new NotSupportedException();

    /// <summary>Zooms out one step around the middle of the stage.</summary>
    internal void ZoomOut() => throw new NotSupportedException();

    /// <summary>Shows all of the world.</summary>
    internal void Fit() => throw new NotSupportedException();

    /// <summary>Pans the least that brings a world rectangle fully into view.</summary>
    /// <param name="target">The rectangle, in world coordinates.</param>
    /// <param name="rightInset">Width of the panel over the stage's right edge.</param>
    internal void Reveal(Rect target, double rightInset = 0) => throw new NotSupportedException();

    /// <summary>Puts the view where an unfinished transition was going, now.</summary>
    internal void Settle() => throw new NotSupportedException();

    /// <summary>Raises <see cref="EmptyClicked"/>.</summary>
    internal void RaiseEmptyClicked() => EmptyClicked?.Invoke(this, EventArgs.Empty);
}

#pragma warning restore MA0182
