using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Host.Commands.Scoping;

/// <summary>What a person hands the desk beside the fact directory: files that become facts the context-reading blocks take (SC-K2).</summary>
internal static class ScopingDeskInputs
{
    /// <summary>The flag that supplies each fact.</summary>
    private static readonly (string Flag, string Fact)[] _files =
    [
        ("--query", "contextQuery"), ("--lenses", "lensesView"), ("--code", "codeView"), ("--history", "historyView"), ("--propose", "proposeResult"),
    ];

    /// <summary>The facts a desk block reads: the directory's files, the out directory's rounds, and the files the flags supply.</summary>
    internal static async Task<Result<Dictionary<string, string>>> Load(ScopingArguments parsed, (string Block, string Directory, string Out) where, (IBriefFiles Files, DateTimeOffset Now) read, CancellationToken ct)
    {
        var facts = await ScopingFactDirectory.Read(where.Directory, ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts;
        }

        await ScopingFactDirectory.OverlayRounds(facts.Value, where.Out, ct).ConfigureAwait(false);
        return await Supply(facts.Value, where.Block, parsed, read, ct).ConfigureAwait(false) is { } refusal ? refusal : facts;
    }

    /// <summary>Adds the flagged files to <paramref name="facts"/>, and for read-brief the guarded brief file; an error when a file cannot be read or the path is refused.</summary>
    internal static async Task<Error?> Supply(Dictionary<string, string> facts, string block, ScopingArguments arguments, (IBriefFiles Files, DateTimeOffset Now) read, CancellationToken ct)
    {
        foreach (var (flag, fact) in _files)
        {
            if (arguments.Value(flag) is not { } file)
            {
                continue;
            }

            if (!File.Exists(file))
            {
                return SindriErrors.InvalidInput.With($"{flag} {file} is not a file (fix: pass the JSON the matching `sindri context ... --json` printed)");
            }

            facts[fact] = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
        }

        if (block is not ("read-brief" or "step.scope.read-brief"))
        {
            return null;
        }

        var roots = new[] { arguments.Value("--repo-root"), arguments.Value("--notes-dir") }.OfType<string>().ToList();
        if (BriefSource.PathOf(ScopeInput(facts.GetValueOrDefault("scopeInput"))) is not { } path)
        {
            return SindriErrors.InvalidInput.With("fact:scopeInput has no path (fix: write scopeInput.json as {\"kind\":\"brief\",\"repo\":\"...\",\"path\":\"...\"})");
        }

        if (roots.Count == 0)
        {
            return SindriErrors.InvalidInput.With("read-brief needs --repo-root <dir> (or --notes-dir <dir>): the directory the brief's path is inside (fix: pass the repo's checkout)");
        }

        var brief = await read.Files.Read(path, roots, ct).ConfigureAwait(false);
        if (brief.IsError)
        {
            return brief.Error;
        }

        facts["briefSource"] = BriefSource.Json(brief.Value, read.Now);
        return null;
    }

    private static JsonNode? ScopeInput(string? json)
    {
        try
        {
            return json is null ? null : JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;   // boundary: the fact is text a person wrote
        }
    }
}
