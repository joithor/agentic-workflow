using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Publishing;

namespace Aw.Evidence.Features.ParityPlan;

/// <summary>The attachment plan.</summary>
/// <param name="Provenance">The run's data provenance (seeded or scrubbed: unknown never gets a plan).</param>
/// <param name="AskFirst">Always true: uploading needs the user's yes.</param>
/// <param name="ReviewBlocked">Whether some image is still unreviewed.</param>
/// <param name="Unreviewed">The images still to open and review.</param>
/// <param name="Plan">One attachment per frame.</param>
public sealed record ParityPlanResponse(Provenance Provenance, bool AskFirst, bool ReviewBlocked, IReadOnlyList<string> Unreviewed, IReadOnlyList<PlannedAttachment> Plan);
