using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The command one action of a row shows: its own catalog row's command with the placeholders the row's subject can fill, filled.
/// The row's primary action and an action the catalog lacks keep the row's served command. The app never invents a command or
/// fills a placeholder it cannot resolve: that one stays as served and the action is marked "fill in".
/// </summary>
internal static partial class ActionCommand
{
    /// <summary>The command to show for an action.</summary>
    /// <param name="catalogCli">The action's command in the catalog; null when the catalog has no row for it.</param>
    /// <param name="isPrimary">True for the row's primary action, whose command the server already wrote for this row.</param>
    /// <param name="rowCli">The row's served command.</param>
    /// <param name="subject">The row's subject, or null.</param>
    /// <param name="itemId">The row's item, or null.</param>
    /// <returns>The command, and whether a required placeholder is still open.</returns>
    internal static (string Cli, bool FillIn) For(string? catalogCli, bool isPrimary, string rowCli, NeedsYouSubject? subject, string? itemId)
    {
        if (isPrimary || string.IsNullOrEmpty(catalogCli))
        {
            return (rowCli, false);
        }

        var filled = Placeholder.Replace(catalogCli, m => Value(m.Groups["word"].Value, subject, itemId) ?? m.Value);
        return (filled, Open.IsMatch(Optional.Replace(filled, "")));
    }

    // <name> is the service's name, <item>, <itemId> and <id> the row's item, <decisionId> a decision's id, <hash> a profile's.
    private static string? Value(string placeholder, NeedsYouSubject? subject, string? itemId) => placeholder switch
    {
        "name" when subject?.Kind == NeedsYouKinds.Service => subject.Id,
        "item" or "itemId" or "id" when !string.IsNullOrEmpty(itemId) => itemId,
        "decisionId" when subject?.DecisionId is { } decision => decision,
        "hash" when subject?.Kind == NeedsYouKinds.Profile => subject.Id,
        _ => null,
    };

    [GeneratedRegex("<(?<word>[A-Za-z]+)>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder { get; }

    [GeneratedRegex(@"\[[^\]]*\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Optional { get; }

    [GeneratedRegex("<[A-Za-z]+>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Open { get; }
}
