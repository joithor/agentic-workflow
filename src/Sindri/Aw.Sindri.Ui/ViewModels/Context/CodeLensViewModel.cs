using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The code lens' two panels (S-9): the callers and callees the code index gave for the files in scope. Empty until a code lens answer is shown.</summary>
internal sealed class CodeLensViewModel
{
    /// <summary>The callers panel, or null before an answer.</summary>
    internal CallsPanelViewModel? Callers { get; private set; }

    /// <summary>The callees panel, or null before an answer.</summary>
    internal CallsPanelViewModel? Callees { get; private set; }

    /// <summary>Takes the two calls objects of a code lens answer.</summary>
    /// <param name="callers">The served callers.</param>
    /// <param name="callees">The served callees.</param>
    internal void Show(SymbolsView callers, SymbolsView callees)
    {
        Callers = new CallsPanelViewModel(callers: true, callers);
        Callees = new CallsPanelViewModel(callers: false, callees);
    }
}
