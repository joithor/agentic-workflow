using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Runs an API script: HTTP requests to the host, then CLI calls, and writes the run's summary.json.</summary>
/// <param name="ScriptPath">The script file (absolute).</param>
/// <param name="RunDirectory">The run directory (absolute); its name is the run id.</param>
/// <param name="At">The side the caller is at; recorded only.</param>
/// <param name="LockTimeout">How long to wait for each machine lock.</param>
/// <param name="Repo">The repository under test (absolute): where build outputs and the targets file are looked for.</param>
/// <param name="Host">The --host origin; required when the script has requests.</param>
/// <param name="AppBuild">The app build (commit) under test, when verified.</param>
/// <param name="AllowPreviewHost">Whether a pr-&lt;n&gt; preview host is allowed.</param>
/// <param name="ConfigPath">A targets file (absolute) used instead of the repo's, when given.</param>
public sealed record ApiRunRequest(
    string ScriptPath,
    string RunDirectory,
    DataRunAt At,
    TimeSpan LockTimeout,
    string Repo,
    string? Host = null,
    string? AppBuild = null,
    bool AllowPreviewHost = false,
    string? ConfigPath = null);
