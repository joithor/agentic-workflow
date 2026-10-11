using Aw.Evidence.Core.Publishing;

namespace Aw.Evidence.Features.PublishPlan;

/// <summary>The publish plan.</summary>
/// <param name="Plan">The effective provenance, the question, whether Linear upload is allowed, the files and the PR comment.</param>
public sealed record PublishPlanResponse(RunPublishPlan Plan);
