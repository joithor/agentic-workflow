using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Usage;

namespace Aw.Evidence.Core.Data;

/// <summary>An api-run's record, written to &lt;run-dir&gt;/summary.json (schema 'evidence.api.summary/1'). Every key is always written.</summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="Kind">Always 'api'.</param>
/// <param name="RunId">The run (the run directory's name).</param>
/// <param name="RunInstance">This invocation, new every time.</param>
/// <param name="StartedAt">When the run started (UTC).</param>
/// <param name="FinishedAt">When it finished (UTC).</param>
/// <param name="AppBuild">The --app-build commit, when given.</param>
/// <param name="ScriptSha256">SHA-256 of the script file.</param>
/// <param name="At">The side the caller named.</param>
/// <param name="Host">The origin the requests went to; null when the script has no requests.</param>
/// <param name="Outcome">How the run ended; agrees with the exit code.</param>
/// <param name="Headline">One line, 200 characters at most, scrubbed.</param>
/// <param name="Steps">Every request, then every CLI call.</param>
/// <param name="Failures">Every assertion that did not hold.</param>
/// <param name="DeclaredWrites">The script's declared write words.</param>
/// <param name="PassedEnv">Names of the variables beyond PATH, HOME, TMPDIR and LANG a CLI call received.</param>
/// <param name="Provenance">'unseeded': the run seeds nothing.</param>
/// <param name="SeedSha256">Always null for api-run.</param>
/// <param name="Refusal">Set when a guard refused the run (exit 1).</param>
/// <param name="Error">Set when the run errored (exit 4).</param>
/// <param name="Invocations">Always empty: no model runs.</param>
public sealed record ApiSummary(
    string Schema,
    string Kind,
    RunId RunId,
    RunInstanceId RunInstance,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string? AppBuild,
    string ScriptSha256,
    DataRunAt At,
    string? Host,
    ApiOutcome Outcome,
    string Headline,
    IReadOnlyList<ApiStep> Steps,
    IReadOnlyList<ApiFailure> Failures,
    IReadOnlyList<string> DeclaredWrites,
    IReadOnlyList<string> PassedEnv,
    string Provenance,
    string? SeedSha256,
    CodedMessage? Refusal,
    CodedMessage? Error,
    IReadOnlyList<ModelInvocation> Invocations)
{
    /// <summary>The schema this build writes.</summary>
    public const string SchemaName = "evidence.api.summary/1";

    /// <summary>The 'kind' an api summary carries.</summary>
    public const string KindName = "api";

    /// <summary>The 'provenance' of a run that seeds nothing.</summary>
    public const string Unseeded = "unseeded";

    /// <summary>The process exit code this summary's outcome stands for.</summary>
    /// <returns>0 passed, 2 failed, 1 refused, 4 errored.</returns>
    public int ExitCode() => Outcome switch
    {
        ApiOutcome.Passed => 0,
        ApiOutcome.Failed => 2,
        ApiOutcome.Refused => 1,
        ApiOutcome.Errored => 4,
        _ => 4,
    };
}
