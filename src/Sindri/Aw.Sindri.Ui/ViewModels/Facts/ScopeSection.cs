namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>One short section under the table: a heading and a line per entry.</summary>
/// <param name="Heading">The section's heading.</param>
/// <param name="Lines">One line per entry.</param>
internal sealed record ScopeSection(string Heading, IReadOnlyList<string> Lines);
