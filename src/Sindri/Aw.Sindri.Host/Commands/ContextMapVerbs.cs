using Aw.Context.Core.Map;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'context map import &lt;file&gt; | export [&lt;path&gt;] | status' (context spec §8.5). Import is a protected command typed
/// like 'profile approve': it shows the file and its hash, asks the human to type the server's challenge ('import map
/// &lt;hash6&gt;') at an interactive terminal, then sends the answer, which the server redeems.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="interactive">Whether stdin and stdout are a terminal.</param>
/// <param name="input">Where the typed confirmation is read.</param>
internal sealed class ContextMapVerbs(ControlPlaneClient client, Output output, bool interactive, TextReader input)
{
    private const string Route = "/sindri/context/map";

    private static readonly string[] _attention =
    [
        "SND-API-001", "SND-AUTH-002", "SND-CONTEXT-003", "SND-CONTEXT-005", "SND-CONTEXT-020", "SND-CONTEXT-030",
        "SND-CONTEXT-031", "SND-CONTEXT-032", "SND-CONTEXT-033", "SND-CONTEXT-034",
    ];

    internal Task<int> Run(string[] args, CancellationToken ct) => args switch
    {
        ["import", var file] => Import(Path.GetFullPath(file), ct),
        ["export", .. var rest] when Options(rest) is { } o => Export(o.Path, o.Json, ct),
        ["status"] => Status(json: false, ct),
        ["status", "--json"] => Status(json: true, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With(
            $"'context map {string.Join(' ', args)}' is not a map command: context map import <file> | context map export [<path>] [--json] | context map status [--json]")),
    };

    private async Task<int> Import(string file, CancellationToken ct)
    {
        var shown = await client.Post<ImportContextMapRequest, ImportContextMapResponse>(Route + "/import", new(file, Hash: null, Answer: null), ct).ConfigureAwait(false);
        if (shown.IsError)
        {
            return await Failed(shown.Error).ConfigureAwait(false);
        }

        var (preview, challenge) = (shown.Value.Value.Preview, shown.Value.Value.Challenge);
        await output.Out.WriteAsync(ContextMapText.Preview(preview)).ConfigureAwait(false);
        var answer = challenge is null
            ? SindriErrors.AuthRefused.With("the server issued no challenge for this import; nothing was imported (fix: update the CLI to the server's build)")
            : await Answer(challenge).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Failed(answer.Error).ConfigureAwait(false);
        }

        var done = await client.Post<ImportContextMapRequest, ImportContextMapResponse>(Route + "/import", new(file, preview.Hash, answer.Value), ct).ConfigureAwait(false);
        return done.IsError ? await Failed(done.Error).ConfigureAwait(false) : await output.Text(ContextMapText.Imported(done.Value.Value), 0).ConfigureAwait(false);
    }

    /// <summary>The human's answer to the challenge: what they typed at the terminal, sent as is (the server compares it).</summary>
    private async Task<Kernel.Result<ChallengeAnswer>> Answer(MapImportChallenge challenge)
    {
        if (!interactive)
        {
            return SindriErrors.AuthRefused.With($"importing a map needs an interactive terminal: run it yourself and type '{challenge.Action}'; nothing was imported");
        }

        await output.Out.WriteAsync($"Type '{challenge.Action}' to import it: ").ConfigureAwait(false);
        var typed = (await input.ReadLineAsync().ConfigureAwait(false))?.Trim() ?? string.Empty;
        return new ChallengeAnswer(challenge.Challenge, typed);
    }

    private async Task<int> Export(string? path, bool json, CancellationToken ct)
    {
        var query = path is null ? string.Empty : "?path=" + Uri.EscapeDataString(path);
        var answer = await client.Get<MapExported>(Route + "/export" + query, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Failed(answer.Error).ConfigureAwait(false);
        }

        return json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false) : await output.Text(ContextMapText.Exported(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Status(bool json, CancellationToken ct)
    {
        var answer = await client.Get<MapStatusView>(Route, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Failed(answer.Error).ConfigureAwait(false);
        }

        return json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false) : await output.Text(ContextMapText.Status(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Failed(Kernel.Error error)
    {
        var code = await output.Error(error).ConfigureAwait(false);
        return _attention.Contains(error.Code, StringComparer.Ordinal) ? 1 : code;
    }

    /// <summary>'[&lt;path&gt;] [--json]' in either order.</summary>
    private static (string? Path, bool Json)? Options(string[] rest) => rest switch
    {
        [] => (null, false),
        ["--json"] => (null, true),
        [var p] when !p.StartsWith("--", StringComparison.Ordinal) => (p, false),
        [var p, "--json"] when !p.StartsWith("--", StringComparison.Ordinal) => (p, true),
        ["--json", var p] when !p.StartsWith("--", StringComparison.Ordinal) => (p, true),
        _ => null,
    };
}
