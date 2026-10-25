namespace Aw.Sindri.Core.Forge;

/// <summary>The checks the forge reports.</summary>
/// <param name="State">The overall state.</param>
/// <param name="Sha">The commit the checks ran on. It can be an older commit than the pull request's head, right after a push.</param>
/// <param name="Failing">The names of the failing checks.</param>
/// <param name="Passed">How many checks passed.</param>
/// <param name="Pending">How many checks have not finished.</param>
public sealed record PullChecks(PullChecksState State, string Sha, IReadOnlyList<string> Failing, int Passed, int Pending);
