using Aw.Kernel;
using Aw.Readiness.Core;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>The JSON-file adapter of <see cref="IRunHistory"/>: one file per run under readiness/runs/&lt;repo&gt;/.</summary>
/// <param name="settings">Where the state directory is.</param>
internal sealed class JsonRunHistory(ReadinessSettings settings) : IRunHistory
{
    public ValueTask<Result<RunRecord?>> Latest(RepoName repo, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: JsonRunHistory under {settings.StateDirectory}");

    public ValueTask<Result<string>> Save(RunRecord run, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: JsonRunHistory.Save");
}
