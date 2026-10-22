using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The code lens' two panels (S-9): the callers and callees the code index gave for the files in scope. Empty until a code lens answer is shown.</summary>
internal sealed class CodeLensViewModel : ObservableObject
{
    private int _generation;

    /// <summary>The callers panel, or null before an answer.</summary>
    public CallsPanelViewModel? Callers { get; private set; }

    /// <summary>The callees panel, or null before an answer.</summary>
    public CallsPanelViewModel? Callees { get; private set; }

    /// <summary>Whether there is an answer to draw.</summary>
    public bool HasPanels => Callers is not null;

    /// <summary>One line saying the code lens could not be read, or null.</summary>
    public string? Notice { get; private set; }

    /// <summary>Whether the notice is shown.</summary>
    public bool HasNotice => Notice is not null;

    /// <summary>Starts a read: any read begun before it is stale from now on.</summary>
    /// <returns>This read's generation.</returns>
    internal int Begin() => ++_generation;

    /// <summary>Whether no later read or clear has happened since <paramref name="generation"/> began.</summary>
    /// <param name="generation">What <see cref="Begin"/> gave.</param>
    /// <returns>True when the read may still draw.</returns>
    internal bool IsCurrent(int generation) => generation == _generation;

    /// <summary>Hides the panels and says the read failed.</summary>
    internal void Fail()
    {
        Clear();
        SetNotice(Labels.ContextCodeUnavailable);
    }

    /// <summary>Takes the two calls objects of a code lens answer.</summary>
    /// <param name="callers">The served callers.</param>
    /// <param name="callees">The served callees.</param>
    internal void Show(SymbolsView callers, SymbolsView callees)
    {
        SetNotice(notice: null);
        Callers = new CallsPanelViewModel(callers: true, callers);
        Callees = new CallsPanelViewModel(callers: false, callees);
        OnChanged(nameof(Callers));
        OnChanged(nameof(Callees));
        OnChanged(nameof(HasPanels));
    }

    /// <summary>Hides the panels: nothing is chosen that the code index answers for.</summary>
    internal void Clear()
    {
        _generation++;
        SetNotice(notice: null);
        if (Callers is null && Callees is null)
        {
            return;
        }

        Callers = null;
        Callees = null;
        OnChanged(nameof(Callers));
        OnChanged(nameof(Callees));
        OnChanged(nameof(HasPanels));
    }

    private void SetNotice(string? notice)
    {
        if (string.Equals(Notice, notice, StringComparison.Ordinal))
        {
            return;
        }

        Notice = notice;
        OnChanged(nameof(Notice));
        OnChanged(nameof(HasNotice));
    }
}
