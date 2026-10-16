namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The sheet of a drifted-profile row (U3' T6): what changed (the served key paths), the profile's hash, and the approve
/// command. Approving is protected: the sheet only shows the command to copy and runs nothing.
/// </summary>
internal sealed class ProfileSheetViewModel
{
    private readonly NeedsYouEntry _entry;

    /// <summary>Shows a profile row.</summary>
    /// <param name="entry">The profile row.</param>
    internal ProfileSheetViewModel(NeedsYouEntry entry)
    {
        _entry = entry;
    }

    /// <summary>The profile's hash as served; empty when the API serves no subject.</summary>
    internal string Hash => _entry.Subject?.Id ?? "";

    /// <summary>The command that approves the profile; the only thing the sheet copies.</summary>
    internal string ApproveCli => _entry.Cli;

    /// <summary>The lines the sheet draws, in order: what changed, the hash (when served), the approve command.</summary>
    internal IReadOnlyList<SheetDetail> Details =>
    [
        new(Labels.SheetChanged, _entry.Reason),
        .. Hash.Length > 0 ? [new SheetDetail(Labels.SheetHash, Hash)] : Array.Empty<SheetDetail>(),
        new(Labels.SheetApprove, ApproveCli),
    ];
}
