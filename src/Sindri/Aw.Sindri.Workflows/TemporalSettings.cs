namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Where the dev server lives, read from the environment Composition passes: the Sindri home by the same rule as the
/// rest of Sindri (AW_SINDRI_HOME, else $AW_STATE_DIR/sindri-app, else ~/.agentic-workflow/sindri-app), and the pinned
/// 'temporal' CLI where 'scripts/binaries-lock.sh fetch temporal' installs it
/// (${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin/temporal-&lt;pin&gt;/temporal). The namespace and the task queue
/// are settings too (README X61): AW_SINDRI_TEMPORAL_NAMESPACE and AW_SINDRI_TEMPORAL_TASK_QUEUE, each 'sindri' when
/// unset or empty. The dev server creates the namespace; the worker polls the queue, and instances start on it.
/// </summary>
/// <param name="Home">The Sindri home; the server's database is &lt;home&gt;/temporal.db.</param>
/// <param name="Executable">The pinned 'temporal' CLI.</param>
internal sealed record TemporalSettings(string Home, string Executable)
{
    /// <summary>The namespace when AW_SINDRI_TEMPORAL_NAMESPACE is unset.</summary>
    public const string DefaultNamespace = "sindri";

    /// <summary>The task queue when AW_SINDRI_TEMPORAL_TASK_QUEUE is unset.</summary>
    public const string DefaultTaskQueue = "sindri";

    /// <summary>The namespace the dev server creates and the worker and clients use.</summary>
    public string Namespace { get; init; } = DefaultNamespace;

    /// <summary>The task queue the worker polls and instances start on.</summary>
    public string TaskQueue { get; init; } = DefaultTaskQueue;

    /// <summary>Reads the settings.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The settings.</returns>
    public static TemporalSettings FromEnvironment(Func<string, string?> environment)
    {
        var root = environment("AW_STATE_DIR") is { Length: > 0 } state
            ? state
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var home = environment("AW_SINDRI_HOME") is { Length: > 0 } explicitHome ? explicitHome : Path.Combine(root, "sindri-app");
        return new TemporalSettings(Path.GetFullPath(home),
            Path.Combine(Path.GetFullPath(root), "cache", "bin", $"temporal-{SindriTemporal.PinnedVersion}", "temporal"))
        {
            Namespace = environment("AW_SINDRI_TEMPORAL_NAMESPACE") is { Length: > 0 } name ? name : DefaultNamespace,
            TaskQueue = environment("AW_SINDRI_TEMPORAL_TASK_QUEUE") is { Length: > 0 } queue ? queue : DefaultTaskQueue,
        };
    }
}
