namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The sheet of a failure row (U3' T6): the service, when it failed, why, and each action the row offers with the command that
/// does it (RunService and PauseService are not in the app yet, so they show their served command to copy).
/// </summary>
internal sealed class FailureSheetViewModel
{
    private readonly NeedsYouEntry _entry;
    private readonly string _age;

    /// <summary>Shows a failure row.</summary>
    /// <param name="entry">The failure row.</param>
    /// <param name="age">How long ago it failed, as the row shows it.</param>
    internal FailureSheetViewModel(NeedsYouEntry entry, string age)
    {
        (_entry, _age) = (entry, age);
    }

    /// <summary>The service that failed (the served subject, else the shown id).</summary>
    internal string Service => _entry.Subject?.Id ?? _entry.ShownId;

    /// <summary>The lines the sheet draws, in order: service, last run, why, then the actions.</summary>
    internal IReadOnlyList<SheetDetail> Details =>
    [
        new(Labels.SheetService, Service),
        new(Labels.SheetFailed, _age),
        new(Labels.SheetWhy, _entry.Reason),
        .. _entry.Actions.Select(a => new SheetDetail(a.Label, a.IsReady ? a.Cli : Labels.NotInTheAppYet(a.Cli))),
    ];
}
