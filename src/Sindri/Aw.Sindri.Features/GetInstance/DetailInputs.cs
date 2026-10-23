using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>What the instance read has gathered for the detail context before the read-time joins.</summary>
/// <param name="Title">The item's title.</param>
/// <param name="Parent">The parent, when this is a child.</param>
/// <param name="Counts">The timeline, decision and request counts.</param>
/// <param name="StepRuns">The Step runs folded from the ledger.</param>
internal sealed record DetailInputs(string Title, ParentRef? Parent, ViewCounts Counts, IReadOnlyList<StepRun> StepRuns);
