using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Runs the Step input named by the context in its own scope.</summary>
/// <param name="context">What the input fills from.</param>
/// <param name="cancellationToken">Cancels the fill.</param>
/// <returns>The sections it filled, or the error that fails the fill.</returns>
internal delegate ValueTask<Kernel.Result<InjectionSections>> InputDispatch(StepInputContext context, CancellationToken cancellationToken);
