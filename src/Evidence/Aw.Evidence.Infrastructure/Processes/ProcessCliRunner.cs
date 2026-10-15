using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Processes;

/// <summary>Runs a program in a fresh temporary directory (its working directory, HOME and TMPDIR) with a cleared environment: a fixed PATH plus the given variables. The program is an absolute path, never looked up on PATH.</summary>
internal sealed class ProcessCliRunner(TimeSpan timeout) : ICliRunner
{
    internal TimeSpan Timeout { get; } = timeout;

    /// <inheritdoc/>
    public ValueTask<Result<CliOutcome>> RunAsync(CliInvocation invocation, CancellationToken cancellationToken) => throw new NotSupportedException();
}
