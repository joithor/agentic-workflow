namespace Aw.Meta.Core;

/// <summary>
/// Which team this run acts on and where its state lives: a named team under teams/&lt;name&gt;, or the legacy flat layout
/// directly under the meta root. Resolved from --team / AW_META_TEAM and what is on disk, never guessed.
/// </summary>
/// <param name="MetaRoot">$AW_STATE_DIR/meta, the directory every team's state lives under.</param>
/// <param name="Team">The selected team's name (or the only team found); null when none is selected and none could be told.</param>
/// <param name="Legacy">Whether the state is the legacy flat layout (team.json and meta.db directly in <paramref name="MetaRoot"/>).</param>
/// <param name="Ambiguous">The team names to choose from when several exist and none is selected; empty otherwise.</param>
/// <param name="Problem">Why the selected team name is unusable; null when it is fine.</param>
public sealed record TeamLayout(string MetaRoot, string? Team, bool Legacy, IReadOnlyList<string> Ambiguous, string? Problem = null)
{
    /// <summary>Whether a team name is a plain lowercase word that can be a directory and a tmux socket name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True for [a-z0-9][a-z0-9-]*.</returns>
    public static bool IsValidName(string name) =>
        name.Length > 0 && (char.IsAsciiDigit(name[0]) || char.IsAsciiLetterLower(name[0])) && name.All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterLower(c) || c == '-');
}
