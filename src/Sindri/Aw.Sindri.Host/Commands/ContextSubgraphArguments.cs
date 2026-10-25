using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'context subgraph &lt;key&gt;... [--keys-from &lt;file|-&gt;] [--depth N] [--map-version V] [--json]': the keys (words, plus one per line of the keys file, '#' comments and blank lines skipped)
/// and the choice of transport: a GET while at most 20 distinct keys fit the URL, an HTTP QUERY with the keys in the body otherwise.
/// </summary>
/// <param name="Keys">The distinct keys, in the order given.</param>
/// <param name="Depth">The depth text as typed (checked, 0 to 4), or null.</param>
/// <param name="MapVersion">A retained map version, or null.</param>
/// <param name="Json">Whether to print the raw answer.</param>
internal sealed record ContextSubgraphArguments(IReadOnlyList<string> Keys, string? Depth, string? MapVersion, bool Json)
{
    /// <summary>Whether the question needs a body: more than 20 keys, or keys that together pass the encoded-text cap once percent-encoded.</summary>
    internal bool UseBody => QueryRules.CheckSubgraphGet(Keys) is not null || Keys.Sum(static k => Uri.EscapeDataString(k).Length) > QueryRules.MaxEncodedText;

    /// <summary>The query string of the GET.</summary>
    internal string QueryString => "?" + string.Join('&', Keys.Select(static k => "nodes=" + Uri.EscapeDataString(k)))
        + (Depth is null ? string.Empty : "&depth=" + Uri.EscapeDataString(Depth)) + (MapVersion is null ? string.Empty : "&mapVersion=" + Uri.EscapeDataString(MapVersion));

    /// <summary>Reads the arguments; a flag it doesn't know, a flag without its value, no key at all or a keys file it can't read is SND-CONTEXT-006.</summary>
    /// <param name="args">The words after 'context subgraph'.</param>
    /// <param name="stdin">Read when '--keys-from -' is given.</param>
    /// <returns>The arguments, or the error.</returns>
    internal static Result<ContextSubgraphArguments> Parse(string[] args, TextReader stdin)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdin);
        var keys = new List<string>();
        var (depth, version, from, json) = ((string?)null, (string?)null, (string?)null, false);
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--depth" or "--map-version" or "--keys-from" when i + 1 >= args.Length:
                    return Invalid($"'{args[i]}' needs a value");
                case "--depth":
                    depth = args[++i];
                    break;
                case "--map-version":
                    version = args[++i];
                    break;
                case "--keys-from":
                    from = args[++i];
                    break;
                case var flag when flag.StartsWith("--", StringComparison.Ordinal):
                    return Invalid($"'{flag}' is not a subgraph flag");
                default:
                    keys.Add(args[i]);
                    break;
            }
        }

        return Finish(keys, from, stdin, (depth, version, json));
    }

    private static Result<ContextSubgraphArguments> Finish(List<string> keys, string? from, TextReader stdin, (string? Depth, string? Version, bool Json) flags)
    {
        if (from is not null)
        {
            var text = ReadKeys(from, stdin);
            if (text.IsError)
            {
                return text.Error;
            }

            keys.AddRange(Lines(text.Value));
        }

        var parsedDepth = QueryRules.ParseDepth(flags.Depth);
        if (parsedDepth.IsError)
        {
            return QueryFlagHints.Add(parsedDepth.Error);
        }

        var distinct = keys.Distinct(StringComparer.Ordinal).ToList();
        return distinct.Count == 0 ? Invalid("name at least one node key (fix: sindri context subgraph feature:a.b, or --keys-from <file|->)")
            : new ContextSubgraphArguments(distinct, flags.Depth, flags.Version, flags.Json);
    }

    private static Result<string> ReadKeys(string from, TextReader stdin)
    {
        if (from == "-")
        {
            return stdin.ReadToEnd();
        }

        try
        {
            return File.ReadAllText(from);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.QueryInvalid.With($"can't read the keys file '{from}' ({e.GetType().Name}) (fix: pass a readable file, or '-' for stdin)");
        }
    }

    private static IEnumerable<string> Lines(string text) => text.Split('\n').Select(static l => l.Trim()).Where(static l => l.Length > 0 && l[0] != '#');

    private static Error Invalid(string reason) => ContextErrors.QueryInvalid.With(reason);
}
