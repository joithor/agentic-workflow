using Aw.Kernel;
using Aw.Readiness.Core;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Testing;

/// <summary>The in-memory fake of <see cref="IRunHistory"/>. <see cref="Unavailable"/> makes every call fail.</summary>
public sealed class InMemoryRunHistory : IRunHistory
{
    private readonly List<RunRecord> _runs = [];

    /// <summary>Every saved run, oldest first.</summary>
    public IReadOnlyList<RunRecord> Runs => _runs;

    /// <summary>When true, reads and writes return AW-READINESS-004.</summary>
    public bool Unavailable { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<RunRecord?>> Latest(RepoName repo, string scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Unavailable
            ? Results.Failure<RunRecord?>(Down())
            : Results.Success(_runs.Where(r => r.Repo == repo && string.Equals(r.Scope, scope, StringComparison.Ordinal))
                .OrderBy(r => r.RecordedAt).LastOrDefault()));   // OrderBy is stable: of two at one instant, the last saved

    /// <inheritdoc/>
    public ValueTask<Result<string>> Save(RunRecord run, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return ValueTask.FromResult(Results.Failure<string>(Down()));
        }

        _runs.Add(run);
        return ValueTask.FromResult(Results.Success(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"memory:{run.Repo}/{_runs.Count}")));
    }

    private static Error Down() => ReadinessErrors.HistoryUnavailable.With("history is down (test); fix: none");
}
