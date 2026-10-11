namespace Aw.BugFix.Core.Model;

/// <summary>The failing run on the unfixed code; every candidate starts from <see cref="Commit"/>.</summary>
/// <param name="Evidence">The run record's full path.</param>
/// <param name="Commit">The commit the run executed against.</param>
public sealed record Baseline(string Evidence, string Commit);
