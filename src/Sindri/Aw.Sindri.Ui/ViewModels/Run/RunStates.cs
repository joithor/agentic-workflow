using Aw.Sindri.Ui.Api.Workflow;

#pragma warning disable MA0182 // until slice 3 places the run page

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The one map from a served chip word to the run canvas's state, and from a state to its word and brush keys (F3 spec 3 §3).</summary>
internal static class RunStates
{
    /// <summary>The state a chip names.</summary>
    /// <param name="chip">The chip as served.</param>
    /// <returns>The state.</returns>
    internal static RunNodeState FromChip(StateChip chip) => throw new NotSupportedException();

    /// <summary>The word for a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The word, in sentence case.</returns>
    internal static string Word(RunNodeState state) => throw new NotSupportedException();

    /// <summary>The brush keys a state is drawn with.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The keys.</returns>
    internal static RunStateBrushes Brushes(RunNodeState state) => throw new NotSupportedException();
}

#pragma warning restore MA0182
