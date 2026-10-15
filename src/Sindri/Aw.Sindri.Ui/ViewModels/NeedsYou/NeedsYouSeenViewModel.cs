using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

#pragma warning disable MA0182 // until the Needs-you list places it (U3' T3)

/// <summary>The Needs-you panel's presentation state: each row's look, the header's pulse, Mark all seen and its Undo.</summary>
internal sealed class NeedsYouSeenViewModel : ObservableObject
{
    internal NeedsYouSeenViewModel(Preferences preferences, IMotionPreference motion, TimeProvider clock)
    {
        throw new NotSupportedException();
    }

    /// <summary>The rows, in the order given.</summary>
    internal IReadOnlyList<NeedsYouRowViewModel> Rows { get; } = [];

    /// <summary>True when the OS asks for reduced motion or the in-app switch is on.</summary>
    internal bool ReducedMotion { get; }

    /// <summary>How many times the panel header has pulsed (once for each batch of arrivals, never under reduced motion).</summary>
    internal int HeaderPulses { get; }

    /// <summary>True while Mark all seen offers Undo.</summary>
    internal bool UndoOffered { get; }

    /// <summary>The Undo line, "N marked seen"; empty when none is offered.</summary>
    internal string UndoText { get; } = "";

    internal Task Restore(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal void SetInAppReduceMotion(bool reduce) => throw new NotSupportedException();

    internal void Update(IReadOnlyList<NeedsYouEntry> entries) => throw new NotSupportedException();

    internal void Watch(string id, bool onScreenAndFocused) => throw new NotSupportedException();

    internal void Acted(string id) => throw new NotSupportedException();

    internal void MarkAllSeen() => throw new NotSupportedException();

    internal void Undo() => throw new NotSupportedException();
}

#pragma warning restore MA0182
