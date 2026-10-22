namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One host after an unanswered poll: how many in a row, and when it is asked again.</summary>
/// <param name="Failures">The polls in a row it did not answer.</param>
/// <param name="NextAt">Not asked before this.</param>
internal sealed record HostBackoff(int Failures, DateTimeOffset NextAt);
