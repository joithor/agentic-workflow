using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Onyx;

// The compose operations: `docker compose -p sindri-onyx -f <compose> -f <override> --env-file <env> <verb>`, arguments as a list.
internal sealed partial class DockerCliRuntime
{
    private const int TailLines = 5;
    private static readonly TimeSpan _pullTimeout = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan _upTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _shortTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions _wire = AwJson.CreateOptions(ComposeWireJsonContext.Default);

    /// <inheritdoc />
    public Result<string> ServerPlatform(CancellationToken cancellationToken)
    {
        var tail = new Tail();
        var run = runner.Run("docker", ["version", "--format", "{{.Server.Os}}/{{.Server.Arch}}"], _timeout, tail.Add, cancellationToken);
        if (Failed(run, tail, "version", _timeout, cancellationToken) is { } error)
        {
            return error;
        }

        var platform = run.Output.Trim().Replace("/aarch64", "/arm64", StringComparison.Ordinal).Replace("/x86_64", "/amd64", StringComparison.Ordinal);
        var parts = platform.Split('/');
        return parts.Length == 2 && parts.All(static p => p.Length > 0 && p.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)))
            ? platform
            : SindriErrors.OnyxComposeFailed.With("docker didn't report a readable server platform (os/architecture); check `docker version`, then run this again");
    }

    /// <inheritdoc />
    public Result<Unit> Pull(ComposeProject project, Action<string> progress, CancellationToken cancellationToken)
        => Compose(project, ["pull"], _pullTimeout, progress, cancellationToken).Map(static _ => Unit.Value);

    /// <inheritdoc />
    public Result<Unit> Up(ComposeProject project, Action<string> progress, CancellationToken cancellationToken)
        => Compose(project, ["up", "-d", "--no-build"], _upTimeout, progress, cancellationToken).Map(static _ => Unit.Value);

    /// <inheritdoc />
    public Result<Unit> Stop(ComposeProject project, CancellationToken cancellationToken)
        => Compose(project, ["stop"], _shortTimeout, progress: null, cancellationToken).Map(static _ => Unit.Value);

    /// <inheritdoc />
    public Result<IReadOnlyList<ComposeContainer>> Ps(ComposeProject project, CancellationToken cancellationToken)
        => Compose(project, ["ps", "-a", "--format", "json"], _shortTimeout, progress: null, cancellationToken).Bind(ParsePs);

    /// <inheritdoc />
    public Result<IReadOnlyDictionary<string, IReadOnlyList<string>>> RepoDigests(IReadOnlyList<string> images, CancellationToken cancellationToken)
    {
        var digests = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var image in images.Distinct(StringComparer.Ordinal))
        {
            var one = RepoDigestsOf(image, cancellationToken);
            if (one.IsError)
            {
                return one.Error;
            }

            digests[image] = one.Value;
        }

        return digests;
    }

    private Result<IReadOnlyList<string>> RepoDigestsOf(string image, CancellationToken cancellationToken)
    {
        if (image.StartsWith('-'))
        {
            return SindriErrors.OnyxComposeFailed.With($"refusing to inspect \"{image}\": an image reference never starts with a dash");
        }

        var tail = new Tail();
        var run = runner.Run("docker", ["image", "inspect", "--format", "{{json .RepoDigests}}", image], _shortTimeout, tail.Add, cancellationToken);
        if (run is { Outcome: ToolOutcome.Exited, ExitCode: not 0 } && !tail.DaemonUnreachable)
        {
            return Results.Success<IReadOnlyList<string>>([]);   // `No such image`: not pulled yet
        }

        if (Failed(run, tail, "image inspect", _shortTimeout, cancellationToken) is { } error)
        {
            return error;
        }

        var parsed = AwJson.Deserialize<List<string>>(Encoding.UTF8.GetBytes(run.Output.Trim() is { Length: > 0 } text ? text : "null"), _wire);
        return parsed.IsError
            ? SindriErrors.OnyxComposeFailed.With($"`docker image inspect` printed something that isn't a list of digests for {image}")
            : Results.Success<IReadOnlyList<string>>([.. (parsed.Value ?? []).Select(ImageNames.WithoutDefaults)]);
    }

    private Result<string> Compose(ComposeProject project, string[] verb, TimeSpan timeout, Action<string>? progress, CancellationToken cancellationToken)
    {
        string[] arguments = ["compose", "-p", ComposeProject.Name, "-f", project.ComposeFile, "-f", project.OverrideFile, "--env-file", project.EnvFile, .. verb];
        var tail = new Tail();
        var run = runner.Run("docker", arguments, timeout, line =>
        {
            tail.Add(line);
            progress?.Invoke(line);
        }, cancellationToken);
        return Failed(run, tail, "compose " + verb[0], timeout, cancellationToken) is { } error ? error : run.Output;
    }

    // The error a finished run stands for, or null when it succeeded. Missing and an unreachable daemon are 001; a command that failed is 011 with what it printed last.
    private static Error? Failed(ToolRun run, Tail tail, string what, TimeSpan timeout, CancellationToken cancellationToken) => run.Outcome switch
    {
        ToolOutcome.Cancelled => throw new OperationCanceledException(cancellationToken),
        ToolOutcome.Missing => SindriErrors.OnyxDockerUnavailable.With("Docker isn't installed: install Docker Desktop (or Docker Engine), then run this again"),
        ToolOutcome.TimedOut when timeout <= _shortTimeout => SindriErrors.OnyxDockerUnavailable.With("Docker isn't answering (a command hung): start or restart Docker Desktop (or the docker service) and wait until it is ready, then run this again"),
        ToolOutcome.TimedOut => SindriErrors.OnyxComposeFailed.With(string.Create(CultureInfo.InvariantCulture, $"`docker {what}` didn't finish within {timeout.TotalMinutes:0} minutes and was stopped; run `sindri onyx up` again to resume")),
        ToolOutcome.Exited when run.ExitCode == 0 => null,
        _ when tail.DaemonUnreachable => SindriErrors.OnyxDockerUnavailable.With("Docker isn't running: start Docker Desktop (or the docker service) and wait until it is ready, then run this again"),
        _ => SindriErrors.OnyxComposeFailed.With(string.Create(CultureInfo.InvariantCulture, $"`docker {what}` failed (exit {run.ExitCode}): {tail.Text()}; fix what it says, then run `sindri onyx up` again to resume")),
    };

    private static Result<IReadOnlyList<ComposeContainer>> ParsePs(string output)
    {
        var text = output.Trim();
        var rows = text.StartsWith('[')
            ? AwJson.Deserialize<List<ComposePsRow>>(Encoding.UTF8.GetBytes(text), _wire)
            : Lines(text);
        return rows.IsError ? Unreadable() : ToContainers(rows.Value);
    }

    private static Result<List<ComposePsRow>> Lines(string text)
    {
        var rows = new List<ComposePsRow>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var row = AwJson.Deserialize<ComposePsRow>(Encoding.UTF8.GetBytes(line), _wire);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return rows;
    }

    private static Result<IReadOnlyList<ComposeContainer>> ToContainers(List<ComposePsRow> rows)
    {
        if (rows.Exists(static r => r.Service is null || r.State is null))
        {
            return Unreadable();
        }

        return Results.Success<IReadOnlyList<ComposeContainer>>([.. rows.Select(static r => new ComposeContainer(
            r.Service!,   // why: checked above
            r.State!,   // why: checked above
            string.IsNullOrEmpty(r.Health) ? null : r.Health,
            r.Image ?? "",
            [.. (r.Publishers ?? []).Where(static p => p.PublishedPort > 0).Select(static p => new ComposePublisher(p.Url ?? "", p.PublishedPort ?? 0, p.TargetPort ?? 0))]))]);
    }

    private static Error Unreadable() => SindriErrors.OnyxComposeFailed.With("`docker compose ps --format json` printed something Sindri can't read; update Docker Compose to v2.21 or newer, then run this again");

    // The last lines a command printed on standard error, for an error message (printable characters only, bounded).
    private sealed class Tail
    {
        private readonly Lock _gate = new();
        private readonly Queue<string> _lines = new();

        public bool DaemonUnreachable { get; private set; }

        public void Add(string line)
        {
            lock (_gate)
            {
                DaemonUnreachable |= line.Contains("Cannot connect to the Docker daemon", StringComparison.OrdinalIgnoreCase) || line.Contains("daemon running", StringComparison.OrdinalIgnoreCase);
                _lines.Enqueue(line);
                if (_lines.Count > TailLines)
                {
                    _ = _lines.Dequeue();
                }
            }
        }

        public string Text()
        {
            lock (_gate)
            {
                var text = string.Join(" | ", _lines.Select(static l => new string([.. l.Where(static c => !char.IsControl(c))]).Trim()).Where(static l => l.Length > 0));
                return text.Length == 0 ? "no output" : text.Length > 800 ? text[^800..] : text;
            }
        }
    }
}
