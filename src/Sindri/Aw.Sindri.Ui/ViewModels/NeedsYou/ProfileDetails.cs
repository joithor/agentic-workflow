namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The labelled lines of a drifted-profile row's sheet (U3' T6): what changed (the served sentence), the profile's hash when served, and
/// the approve command. Approving is protected: the sheet only shows the command to copy and runs nothing.
/// </summary>
internal static class ProfileDetails
{
    /// <summary>The lines, in order: what changed, the hash (when served), the approve command.</summary>
    /// <param name="entry">The profile row.</param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<SheetDetail> Build(NeedsYouEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var hash = entry.Subject?.Id ?? "";
        return
        [
            new(Labels.SheetChanged, entry.Reason),
            .. hash.Length > 0 ? [new SheetDetail(Labels.SheetHash, hash, Secondary: true)] : Array.Empty<SheetDetail>(),
            new(Labels.SheetApprove, entry.Cli, Secondary: true),
        ];
    }
}
