using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The profile's approval state.</summary>
/// <param name="Approved">The approved hash the process runs with, if any.</param>
/// <param name="Live">The live file's hash, when it loads.</param>
/// <param name="ChangedSinceApproval">True when the live file differs from the approval.</param>
/// <param name="Problem">Why the live file doesn't load, if it doesn't.</param>
/// <param name="Source">The live profile file, when it loads.</param>
/// <param name="NeedsApproval">True when nothing is approved yet or the live file changed since the approval.</param>
public sealed record ProfileStatus(ProfileHash? Approved, ProfileHash? Live, bool ChangedSinceApproval, string? Problem, string? Source, bool NeedsApproval);
