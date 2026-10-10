using Aw.Readiness.Core;
using Aw.Readiness.Core.Scoring;

namespace Aw.Readiness.App.Features.CheckRepo;

/// <summary>The CheckRepo response: the report of record (spec §8; '--json' writes it as is).</summary>
/// <param name="SchemaVersion">The report schema version (<see cref="CurrentSchemaVersion"/>).</param>
/// <param name="Repo">The repo, from readiness.yaml.</param>
/// <param name="Scope">What was checked.</param>
/// <param name="RecordedAt">When the run happened (UTC).</param>
/// <param name="Target">The target for the repo's ring.</param>
/// <param name="Level">The overall level reached.</param>
/// <param name="Holds">True when the level reaches the target. Unread criteria count as not passing, so the level is a
/// lower bound and a target it reaches holds whatever they turn out to be.</param>
/// <param name="Unconfirmed">When the target doesn't hold: the criteria below its level whose data wasn't read
/// (offline, gh failing), which could still raise the level once read. Empty when it holds.</param>
/// <param name="RecordedTo">Where the run was recorded.</param>
/// <param name="Delta">What changed since the previous run.</param>
/// <param name="Pillars">The level per pillar.</param>
/// <param name="Tallies">The overall tallies per level.</param>
/// <param name="Findings">Every criterion's state and evidence.</param>
internal sealed record CheckRepoResponse(
    int SchemaVersion,
    RepoName Repo,
    string Scope,
    DateTimeOffset RecordedAt,
    ReadinessTarget Target,
    int Level,
    bool Holds,
    IReadOnlyList<CriterionId> Unconfirmed,
    string RecordedTo,
    RunDelta Delta,
    IReadOnlyList<PillarScore> Pillars,
    IReadOnlyList<LevelTally> Tallies,
    IReadOnlyList<Finding> Findings)
{
    /// <summary>The report schema version this build writes.</summary>
    internal const int CurrentSchemaVersion = 1;
}
