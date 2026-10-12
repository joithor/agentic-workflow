using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// A state glyph (UI spec §11.3) or one of the shell's marks, drawn as a vector icon from Theme/Icons.axaml (R1): the
/// geometry sits on a 16 × 16 grid and is scaled to the control's size (Size.Icon, the Body line, scaled with the text)
/// and stroked at 1.5 px with round caps; the brand mark is filled. It never draws a font glyph, so it renders the same
/// on every OS. A state icon is decorative: the word beside it carries the state, so it is hidden from assistive tech.
/// The :ok, :down and :stale pseudo-classes follow <see cref="Glyph"/> for the icon's colour styles.
/// </summary>
internal sealed class StateIcon : Control
{
    /// <summary>The state glyph to draw; ignored when <see cref="Icon"/> names a mark.</summary>
    public static readonly StyledProperty<StateGlyph> GlyphProperty = AvaloniaProperty.Register<StateIcon, StateGlyph>(nameof(Glyph));

    /// <summary>A mark to draw by its Icons.axaml name ("Brand", "Refresh", "Close", "Command", "Spinner").</summary>
    public static readonly StyledProperty<string?> IconProperty = AvaloniaProperty.Register<StateIcon, string?>(nameof(Icon));

    /// <summary>The brush the icon is drawn with (its styles set a Brush.* token).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<StateIcon, IBrush?>(nameof(Foreground));

    /// <summary>The geometry drawn, resolved from Icons.axaml.</summary>
    public static readonly StyledProperty<Geometry?> GeometryProperty = AvaloniaProperty.Register<StateIcon, Geometry?>(nameof(Geometry));

    // The grid every icon is drawn on, and its stroke.
    private const double Grid = 16;
    private const double Stroke = 1.5;

    static StateIcon()
    {
        AffectsRender<StateIcon>(ForegroundProperty, GeometryProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<StateIcon>(AccessibilityView.Raw);
    }

    /// <summary>Gets or sets the state glyph.</summary>
    public StateGlyph Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>Gets or sets the mark's name.</summary>
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Gets or sets the icon's brush.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets a value indicating whether the icon is turning (the spinner, unless motion is reduced).</summary>
    public bool IsSpinning => Bounds.Width < 0;

    /// <summary>Gets the geometry drawn, or null when there is nothing to draw (no glyph).</summary>
    public Geometry? Geometry => GetValue(GeometryProperty);

    // The resource key for what this icon draws, or null for none.
    private string? Key => Icon is { Length: > 0 } icon ? "Icon." + icon : Glyph == StateGlyph.None ? null : "Icon." + Glyph;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Geometry is not { } geometry || Foreground is not { } brush)
        {
            return;
        }

        var scale = Math.Min(Bounds.Width, Bounds.Height) / Grid;
        var offset = new Vector((Bounds.Width - (Grid * scale)) / 2, (Bounds.Height - (Grid * scale)) / 2);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset)))
        {
            if (string.Equals(Icon, "Brand", StringComparison.Ordinal))
            {
                context.DrawGeometry(brush, pen: null, geometry);
            }
            else
            {
                context.DrawGeometry(brush: null, new Pen(brush, Stroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Resolve();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphProperty || change.Property == IconProperty)
        {
            PseudoClasses.Set(":ok", Glyph == StateGlyph.Ok);
            PseudoClasses.Set(":down", Glyph == StateGlyph.Down);
            PseudoClasses.Set(":stale", Glyph == StateGlyph.Stale);
            Resolve();
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    private void Resolve() =>
        SetValue(GeometryProperty, Key is { } key && this.TryFindResource(key, out var value) ? value as Geometry : null);
}
