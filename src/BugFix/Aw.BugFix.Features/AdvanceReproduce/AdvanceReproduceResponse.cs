using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.AdvanceReproduce;

/// <summary>The result of advance reproduce.</summary>
/// <param name="Phase">The new phase.</param>
/// <param name="Check">The check, repository-relative.</param>
/// <param name="BaselineCommit">The baseline commit every candidate starts from.</param>
public sealed record AdvanceReproduceResponse(
    Phase Phase,
    string Check,
    string BaselineCommit);
