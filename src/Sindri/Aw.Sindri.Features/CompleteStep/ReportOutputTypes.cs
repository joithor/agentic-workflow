using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>Checks a launched agent's reported outputs against the types its recipe declares.</summary>
internal sealed class ReportOutputTypes
{
    /// <summary>Checks the report.</summary>
    /// <param name="view">The instance as read.</param>
    /// <param name="request">The report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The refusal, or null when every declared output holds.</returns>
    public ValueTask<Error?> Check(InstanceView view, CompleteStepRequest request, CancellationToken cancellationToken) => throw new NotSupportedException("red");
}
