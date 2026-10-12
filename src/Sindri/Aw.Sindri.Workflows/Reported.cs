using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the interpreter's loop did with a desk report, handed to the 'complete' Update that carried it (engine P4 unit (c)).</summary>
/// <param name="Result">The instance after it and what happened, when it was applied.</param>
/// <param name="Refusal">Why it was refused, when it wasn't.</param>
internal sealed record Reported(CommandResult? Result, Error? Refusal);
