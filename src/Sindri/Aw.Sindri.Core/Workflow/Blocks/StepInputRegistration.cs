using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>Marks that this build has a runtime for the Step input <paramref name="Id"/> (AddStepInput registers one per input class).</summary>
/// <param name="Id">The input block's id.</param>
public sealed record StepInputRegistration(BlockId Id);
