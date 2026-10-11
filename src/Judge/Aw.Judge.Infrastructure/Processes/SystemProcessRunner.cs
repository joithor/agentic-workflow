using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;

namespace Aw.Judge.Infrastructure.Processes;

/// <summary>IProcessRunner over System.Diagnostics.Process.</summary>
/// <param name="environment">Reads PATH, to find a program given by bare name.</param>
internal sealed class SystemProcessRunner(Func<string, string?> environment) : IProcessRunner
{
    /// <inheritdoc />
    public ValueTask<ProcessResult> Run(ProcessRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"not implemented yet ({environment("PATH")})");
}
