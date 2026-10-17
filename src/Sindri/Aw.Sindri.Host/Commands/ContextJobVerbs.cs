using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'context sources sync [&lt;source&gt;]' and 'context map build' (API P6 follow-up): each starts its job and follows it through the
/// <see cref="JobFollower"/> as 'services run' does (Ctrl-C cancels the job, a second Ctrl-C leaves); --detach prints the job id and returns, --json
/// prints the JobStarted body then the final job. Exit codes are the follower's: 0 succeeded, 1 failed, cancelled, left or not running, 2 refused or invalid.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the job a start answers.</param>
internal sealed class ContextJobVerbs(ControlPlaneClient client, Output output, JobFollower follower)
{
    internal const string SyncUsage = "context sources sync [<source>] [--detach] [--json]";
    internal const string BuildUsage = "context map build [--detach] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        var sync = args is ["sources", ..];
        var line = Parse(args[2..], sync ? 1 : 0);
        if (line is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'context {string.Join(' ', args)}' isn't this command: {(sync ? SyncUsage : BuildUsage)}")).ConfigureAwait(false);
        }

        // the start itself is never cancelled by Ctrl-C (as 'services run'): the job may exist on the server, and only the follow listens for the interrupt
        var started = sync
            ? await client.Post<SyncContextSourcesRequest, JobStartedResponse>("/sindri/context/sources/sync", new SyncContextSourcesRequest(line.Words.Count == 1 ? line.Words[0] : null), CancellationToken.None).ConfigureAwait(false)
            : await client.Post<BuildContextMapRequest, JobStartedResponse>("/sindri/context/map/build", new BuildContextMapRequest(), CancellationToken.None).ConfigureAwait(false);
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        if (!line.Detach)
        {
            return await follower.Follow(started.Value.Value, line.Json, interrupt, again).ConfigureAwait(false);
        }

        return line.Json
            ? await output.Raw(started.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"job {started.Value.Value.JobId.Value} started\n", 0).ConfigureAwait(false);
    }

    /// <summary>The words (at most <paramref name="maxWords"/>) and the --detach and --json flags; null for anything else.</summary>
    private static ContextJobLine? Parse(string[] rest, int maxWords)
    {
        var flags = rest.Where(static a => a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var words = rest.Where(static a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var known = flags.TrueForAll(static f => string.Equals(f, "--detach", StringComparison.Ordinal) || string.Equals(f, "--json", StringComparison.Ordinal));
        return known && words.Count <= maxWords ? new ContextJobLine(words, flags.Contains("--detach", StringComparer.Ordinal), flags.Contains("--json", StringComparer.Ordinal)) : null;
    }

    private sealed record ContextJobLine(IReadOnlyList<string> Words, bool Detach, bool Json);
}
