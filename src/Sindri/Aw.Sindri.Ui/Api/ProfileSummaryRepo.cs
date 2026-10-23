namespace Aw.Sindri.Ui.Api;

/// <summary>One repo of the approval's summary.</summary>
/// <param name="Name">The repo's name in the profile.</param>
/// <param name="Path">The real path Sindri would read.</param>
/// <param name="Files">The tracked file count.</param>
/// <param name="Megabytes">The tracked size in MiB, one decimal.</param>
/// <param name="Sentences">What approving lets Sindri do with this repo.</param>
internal sealed record ProfileSummaryRepo(string Name, string Path, long Files, double Megabytes, IReadOnlyList<string> Sentences);
