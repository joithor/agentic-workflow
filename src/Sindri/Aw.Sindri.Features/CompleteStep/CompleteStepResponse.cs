using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>What 'workflow complete' did (engine spec §5.2, §6.3): the instance after it and what happened, the nonce it used, and whether that nonce had already applied this same report.</summary>
/// <param name="Result">The instance after the report, and what happened ('-> checks').</param>
/// <param name="Nonce">The nonce the command used (printed with '--json').</param>
/// <param name="AlreadyApplied">Whether the same report was already applied with this nonce, so nothing new happened.</param>
public sealed record CompleteStepResponse(CommandResult Result, CommandNonce Nonce, bool AlreadyApplied);
