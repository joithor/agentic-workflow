using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The launch's files and argv (the part of <see cref="Launcher"/> that prepares a start and cleans up after one).</summary>
internal sealed partial class Launcher
{
    private async ValueTask<Result<Prepared>> Prepare(LaunchRequest request, AgentEntry entry, CancellationToken cancellationToken)
    {
        string workDir;
        if (request.Evaluator is { } evaluator)
        {
            if (request.Attended || request.Step?.Policy is null)
            {
                return SindriErrors.RequestInvalid.With("the holdout evaluator launches unattended and with a policy (fix: build the request with AgentLaunches.RequestFor)");
            }

            // The evaluator never runs in the phase worktree: its own checkout at the head sha, removed with the launch (LN-LCH2).
            var scratch = await parts.Scratch.Create(request.RepoPath, ScratchPath(request.Launch), evaluator.HeadSha, cancellationToken).ConfigureAwait(false);
            if (scratch.IsError)
            {
                return scratch.Error;
            }

            workDir = scratch.Value;
        }
        else
        {
            if (await LaunchWorktree.Ensure(request, cancellationToken).ConfigureAwait(false) is { } missing)
            {
                return missing;
            }

            workDir = request.Worktree ?? request.RepoPath;
        }

        try
        {
            var composed = await Compose(request, entry, workDir, cancellationToken).ConfigureAwait(false);
            if (composed.IsError)
            {
                return composed.Error;
            }

            WriteCaptureRule(request.Launch, entry);
            var secret = LaunchSecrets.Mint();
            var hashFile = LaunchSecrets.PathFor(parts.Options.SecretsDirectory, request.Launch);
            var prior = File.Exists(hashFile) ? await File.ReadAllBytesAsync(hashFile, cancellationToken).ConfigureAwait(false) : null;   // a live session of this launch may own it
            LaunchSecrets.Write(parts.Options.SecretsDirectory, request.Launch, secret);
            return new Prepared(workDir, composed.Value.Argv, Environment(request), composed.Value.Session?.Value) { PriorHash = prior };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return KernelErrors.Unavailable.With($"the launch's files could not be written: {e.Message}");
        }
    }

    private async ValueTask<Result<(IReadOnlyList<string> Argv, SessionId? Session)>> Compose(LaunchRequest request, AgentEntry entry, string workDir, CancellationToken cancellationToken)
    {
        var step = request.Attended ? null : request.Step;
        if (step?.Policy is { } requested && request.Evaluator is not null)
        {
            step = step with { Policy = requested with { WritablePath = workDir } };   // the scratch checkout is the one path the evaluator writes
        }

        var policyPath = step?.Policy is { } policy && entry.PolicyForm == PolicyForm.ClaudeSettingsFile
            ? StepPolicyFile.Write(parts.Options.PolicyDirectory, request.Launch, policy)
            : null;
        var session = request.Mode == LaunchMode.Resume && request.Resume is { } resumed ? resumed : PreChosen(entry);
        var seed = ArgvBuilder.Seed(request.Item) + StepSeed(step);
        var (mcpPath, mcpArguments) = McpLaunchConfig.Prepare(entry.McpForm, entry.McpForm == McpForm.None ? null : await parts.McpFor(request.RepoPath, cancellationToken).ConfigureAwait(false), parts.Options.LaunchDirectory, request.Launch);
        var built = ArgvBuilder.Build(entry, request.Mode, new ArgvInputs(request.Item, workDir, SessionName(request.Launch), seed, session, mcpPath, step, policyPath));
        if (built.IsError)
        {
            return built.Error;
        }

        IReadOnlyList<string> argv = [.. built.Value, .. mcpArguments];
        return (argv, session);
    }

    /// <summary>What an unattended launch adds to the seed: which Step it is, then the Step's brief when it has one (engine P8 F13).</summary>
    private static string StepSeed(StepArgs? step) =>
        step is null ? string.Empty : $" This is the {step.Ref.State} step, block {step.Ref.Block}." + (step.Brief is { Length: > 0 } brief ? " " + brief : string.Empty);

    /// <summary>The session id an entry in 'pre-chosen-uuid' mode is told to use, else none (an id reported later is recorded then, launcher L-9).</summary>
    private static SessionId? PreChosen(AgentEntry entry) => entry.SessionIdMode == SessionIdMode.PreChosenUuid ? SessionId.From(Guid.NewGuid().ToString("D")) : null;

    /// <summary>The capture rule 'session wrap' reads for an entry whose CLI reports its own id (launcher L-9): 0600, no secret in it.</summary>
    private void WriteCaptureRule(LaunchId launch, AgentEntry entry)
    {
        if (entry.SessionIdMode != SessionIdMode.Reported)
        {
            return;
        }

        OwnerOnly.EnsureDirectory(parts.Options.LaunchesDirectory);
        var rule = new CaptureRule(entry.SessionIdPattern, entry.ReportedNewestFile, entry.IdCapture);
        OwnerOnly.WriteAtomically(Path.Combine(parts.Options.LaunchesDirectory, LaunchFiles.CaptureName(launch)), AwJson.Serialize(rule, _captureJson));
    }

    private Dictionary<string, string> Environment(LaunchRequest request)
    {
        var env = new Dictionary<string, string>(StepEnvironment.For(request), StringComparer.Ordinal)
        {
            ["AW_SINDRI_CHILD"] = "1",
            ["AW_SINDRI_ITEM"] = request.Item.Value,
            ["AW_SINDRI_LAUNCH"] = request.Launch.Value,
            ["AW_SINDRI_LAUNCH_SECRET_FILE"] = LaunchSecrets.SecretPathFor(parts.Options.SecretsDirectory, request.Launch),   // the path only: the secret itself never sits in a tmux command line
        };
        if (parts.Options.SindriHome is { } home)
        {
            env["AW_SINDRI_HOME"] = home;
        }

        return env;
    }

    private async ValueTask Cleanup(LaunchId launch)
    {
        LaunchSecrets.Delete(parts.Options.SecretsDirectory, launch);
        LaunchFileStore.DeleteFlat(parts.Options, launch);
        LaunchFileStore.DeleteIn(parts.Options.PolicyDirectory, StepPolicyFile.PathFor(parts.Options.PolicyDirectory, launch));
        LaunchFileStore.DeleteIn(parts.Options.LaunchDirectory, McpLaunchConfig.PathFor(parts.Options.LaunchDirectory, launch));
        _ = await parts.Scratch.Remove(ScratchPath(launch), CancellationToken.None).ConfigureAwait(false);   // the evaluator's checkout; none for any other launch
    }

    private string ScratchPath(LaunchId launch) => Path.Combine(parts.Options.ScratchDirectory, "eval-" + launch.Value);

    private sealed record Prepared(string WorkDir, IReadOnlyList<string> Argv, Dictionary<string, string> Env, string? Session)
    {
        /// <summary>The hash file as it was before this start rewrote it, or null when there was none.</summary>
        public byte[]? PriorHash { get; init; }
    }

    /// <summary>The start clashed with a session of this launch's name: it is the launch's own and stays untouched. The hash the live wrap checks is put back (and the session answered as started), or, with no hash to put back, the minted pair is removed and the launch fails.</summary>
    private LaunchResult Clash(LaunchRequest request, AgentEntry entry, ISessionHost host, Prepared prepared, Error clash)
    {
        LaunchSecrets.Delete(parts.Options.SecretsDirectory, request.Launch);
        if (prepared.PriorHash is not { } prior)
        {
            return Fail(clash);
        }

        OwnerOnly.WriteAtomically(LaunchSecrets.PathFor(parts.Options.SecretsDirectory, request.Launch), prior);
        var session = new HostRef(host.Kind, SessionName(request.Launch));
        return new StartedLaunch(session, ViewerOutcome.Skipped, host.AttachArgv(session), entry.Source, entry.ArgvHash ?? string.Empty);
    }
}
