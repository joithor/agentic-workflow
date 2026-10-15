using Aw.Context.Core.Lenses;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context lenses &lt;anchor&gt; [--draft] [--json]' and 'sindri context filemap --repo R [--path P] [--unmapped] [--draft] [--json]'
/// (context spec §8.5, §8.9): clients of GET /sindri/context/lenses and /filemap. Exit 0 with an answer; 1 for attention (not running,
/// unknown anchor, no draft, a repo outside the scope, sessions off); 2 for a tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class ContextLensVerbs(ControlPlaneClient client, Output output)
{
    private const string Forms = "context lenses <anchor> [--draft] [--json] | context filemap --repo <repo> [--path <dir>] [--unmapped] [--draft] [--json] | context filemap accept|reject <link-id>";

    internal Task<int> Run(string[] args, CancellationToken ct) => args switch
    {
        ["lenses", .. var rest] when Lenses(rest) is { } l => Lenses(l, ct),
        ["filemap", .. var rest] when FileMap(rest) is { } f => FileMap(f, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'context {string.Join(' ', args)}' is not a lenses or filemap command: {Forms}")),
    };

    private async Task<int> Lenses((string Anchor, bool Draft, bool Json) o, CancellationToken ct)
    {
        var answer = await client.Get<LensesView>("/sindri/context/lenses?anchor=" + Uri.EscapeDataString(o.Anchor) + (o.Draft ? "&draft=true" : string.Empty), ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextLensText.Lenses(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> FileMap((string Repo, string? Path, bool Unmapped, bool Draft, bool Json) o, CancellationToken ct)
    {
        var query = "?repo=" + Uri.EscapeDataString(o.Repo) + (o.Path is null ? string.Empty : "&path=" + Uri.EscapeDataString(o.Path))
            + (o.Unmapped ? "&unmapped=true" : string.Empty) + (o.Draft ? "&draft=true" : string.Empty);
        var answer = await client.Get<FileMapView>("/sindri/context/filemap" + query, ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextLensText.FileMap(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Failed(Kernel.Error error) => ContextExit.For(error, await output.Error(error).ConfigureAwait(false));

    /// <summary>'&lt;anchor&gt;' with [--draft] and [--json] in any order.</summary>
    private static (string Anchor, bool Draft, bool Json)? Lenses(string[] rest)
    {
        var flags = Flags(rest, valued: [], bare: ["--draft", "--json"]);
        return flags is { Words: [var anchor] } ? (anchor, flags.Bare.Contains("--draft"), flags.Bare.Contains("--json")) : null;
    }

    /// <summary>'--repo R' with [--path P] [--unmapped] [--draft] [--json] in any order.</summary>
    private static (string Repo, string? Path, bool Unmapped, bool Draft, bool Json)? FileMap(string[] rest)
    {
        var flags = Flags(rest, valued: ["--repo", "--path"], bare: ["--unmapped", "--draft", "--json"]);
        return flags is { Words: [], Values: var v } && v.TryGetValue("--repo", out var repo)
            ? (repo, v.GetValueOrDefault("--path"), flags.Bare.Contains("--unmapped"), flags.Bare.Contains("--draft"), flags.Bare.Contains("--json"))
            : null;
    }

    /// <summary>Sorts the arguments into words, valued flags and bare flags; null for an unknown flag, a repeated one or one without its value.</summary>
    private static ParsedFlags? Flags(string[] args, string[] valued, string[] bare)
    {
        var words = new List<string>();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (valued.Contains(arg, StringComparer.Ordinal))
            {
                if (i + 1 >= args.Length || !values.TryAdd(arg, args[++i]))
                {
                    return null;
                }
            }
            else if (bare.Contains(arg, StringComparer.Ordinal))
            {
                _ = set.Add(arg);
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }
            else
            {
                words.Add(arg);
            }
        }

        return new ParsedFlags([.. words], values, set);
    }

    private sealed record ParsedFlags(string[] Words, Dictionary<string, string> Values, HashSet<string> Bare);
}
