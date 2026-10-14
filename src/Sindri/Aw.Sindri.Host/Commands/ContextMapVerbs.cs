using Aw.Context.Core.Map;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'context map import &lt;file&gt; | export [&lt;path&gt;] | status' (context spec §8.5). Import is shaped like 'profile approve':
/// it shows the file's hash, asks for its first 6 characters at an interactive terminal, then imports.
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
        var shown = await client.Post<ImportContextMapRequest, ImportContextMapResponse>(Route + "/import", new(file, Hash: null, Confirmed: false), ct).ConfigureAwait(false);
        if (shown.IsError)
        {
            return await Failed(shown.Error).ConfigureAwait(false);
        }

        var preview = shown.Value.Value.Preview;
        await output.Out.WriteAsync(ContextMapText.Preview(preview)).ConfigureAwait(false);
        if (await Confirmation(preview.Hash).ConfigureAwait(false) is { } refused)
        {
            return await Failed(refused).ConfigureAwait(false);
        }

        var done = await client.Post<ImportContextMapRequest, ImportContextMapResponse>(Route + "/import", new(file, preview.Hash, Confirmed: true), ct).ConfigureAwait(false);
        return done.IsError ? await Failed(done.Error).ConfigureAwait(false) : await output.Text(ContextMapText.Imported(done.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<Kernel.Error?> Confirmation(string hash)
    {
        if (!interactive)
        {
            return SindriErrors.AuthRefused.With("importing a map needs an interactive terminal: run it yourself and type the first 6 characters of the hash; nothing was imported");
        }

        await output.Out.WriteAsync($"Type the first 6 characters of {hash} to import it: ").ConfigureAwait(false);
        var typed = (await input.ReadLineAsync().ConfigureAwait(false))?.Trim();
        return string.Equals(typed, hash[..6], StringComparison.Ordinal)
            ? null
            : SindriErrors.AuthRefused.With($"you typed '{typed}', not the hash's first 6 characters; nothing was imported");
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
