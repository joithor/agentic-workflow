using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// 'workflow complete' from the desk (engine spec rev 8.1 §5.3, §6.3 'POST /workflow/instances/{id}/runs/{run}/complete'):
/// the report for an open desk run. Outputs travel as content, at most 1 MiB each; the server never opens a client path.
/// </summary>
/// <param name="Instance">The instance: its id ('WFI-…', what the route carries), its address, or a bare item.</param>
/// <param name="Run">The open run's token, as status shows it.</param>
/// <param name="Outcome">One of the Step's declared outcomes.</param>
/// <param name="Outputs">Each output fact's content.</param>
/// <param name="Nonce">The command's nonce; null to have one issued.</param>
public sealed record CompleteStepRequest(
    string Instance,
    RunToken Run,
    OutcomeName Outcome,
    IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> Outputs,
    CommandNonce? Nonce = null) : ICommand<CompleteStepResponse>;
