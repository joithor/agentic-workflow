using Aw.Kernel;
using Aw.Sindri.Core;

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
    internal static async Task<Result<Dictionary<string, string>>> Load(ScopingArguments parsed, (string Block, string Directory, string Out) where, DateTimeOffset now, CancellationToken ct)
    {
        var facts = await ScopingFactDirectory.Read(where.Directory, ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts;
        }

        await ScopingFactDirectory.OverlayRounds(facts.Value, where.Out, ct).ConfigureAwait(false);
        return await Supply(facts.Value, where.Block, parsed, now, ct).ConfigureAwait(false) is { } refusal ? refusal : facts;
    }

    /// <summary>Adds the flagged files to <paramref name="facts"/>, and for read-brief the guarded brief file; an error when a file cannot be read or the path is refused.</summary>
    internal static async Task<Error?> Supply(Dictionary<string, string> facts, string block, ScopingArguments arguments, DateTimeOffset now, CancellationToken ct)
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
        var source = await ScopingBriefFile.Source(facts.GetValueOrDefault("scopeInput") ?? "{}", roots, now, ct).ConfigureAwait(false);
        if (source.IsError)
        {
            return source.Error;
        }

        facts["briefSource"] = source.Value;
        return null;
    }
}
