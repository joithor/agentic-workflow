using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Runs the vetted CLI calls in order, each in its own temporary directory with an exact environment, and checks each expectation.</summary>
internal sealed class CallRunner(ICliRunner runner, ITextScrubber scrubber)
{
    internal (ICliRunner, ITextScrubber) Collaborators { get; } = (runner, scrubber);
}
