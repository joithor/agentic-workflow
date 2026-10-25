using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>One Step registered with AddBlockStep.</summary>
/// <param name="Step">The Step's id.</param>
/// <param name="NeedsRepo">Whether the Step reads the instance's repo (it is an <see cref="IRepoStep"/>).</param>
public sealed record BlockStepRegistration(BlockId Step, bool NeedsRepo = false);
