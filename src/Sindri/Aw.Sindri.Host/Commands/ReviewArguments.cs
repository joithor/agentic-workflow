using Aw.Sindri.Core;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The arguments of 'sindri context map review' (context spec §8.7) and of 'sindri context filemap accept|reject': a pure parse, so
/// a mistake is a usage error before anything is sent.
/// </summary>
internal static class ReviewArguments
{
    private const string Forms = "context map review [--json] | review --history | review --export [<path>] | review --apply <file> | review --undo <id> | review --drop <id> | "
        + "review --repoint <id> --to <node> | review --accept-deterministic | review accept|reject <node|link> | review rename <node> <name…> | review move <node> --under <node> | "
        + "review merge <node> --into <node> | review add <node> <name…> [--under <node>] | review delete <node>";

    /// <summary>Reads the arguments after 'review'.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The command, or SND-CLI-001 naming the forms.</returns>
    internal static Kernel.Result<ReviewCommand> Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var json = args.Contains("--json", StringComparer.Ordinal);
        string[] rest = [.. args.Where(a => !string.Equals(a, "--json", StringComparison.Ordinal))];
        ReviewCommand Send(ReviewContextMapRequest request) => new(ReviewMode.Request, json, request);
        var command = rest switch
        {
            [] => new ReviewCommand(json ? ReviewMode.Queue : ReviewMode.Interactive, json),
            ["--list"] => new ReviewCommand(ReviewMode.Queue, json),
            ["--history"] => new ReviewCommand(ReviewMode.History, json),
            ["--export"] => new ReviewCommand(ReviewMode.Export, json),
            ["--export", var path] when !path.StartsWith("--", StringComparison.Ordinal) => new ReviewCommand(ReviewMode.Export, json, Path: path),
            ["--apply", var file] => Send(new ReviewContextMapRequest("apply-file", Path: Path.GetFullPath(file))),
            ["--undo", var id] => Send(new ReviewContextMapRequest("undo", Override: id)),
            ["--drop", var id] => Send(new ReviewContextMapRequest("drop", Override: id)),
            ["--accept-deterministic"] => Send(new ReviewContextMapRequest("accept-deterministic")),
            ["--repoint", var id, "--to", var node] => Send(new ReviewContextMapRequest("repoint", Node: node, Override: id)),
            ["accept" or "reject", var target] => Send(Decide(rest[0], target)),
            ["rename", var node, .. var words] when words.Length > 0 => Send(new ReviewContextMapRequest("rename", Node: node, Label: string.Join(' ', words))),
            ["move", var node, "--under", var parent] => Send(new ReviewContextMapRequest("move", Node: node, Other: parent)),
            ["merge", var node, "--into", var into] => Send(new ReviewContextMapRequest("merge", Node: node, Other: into)),
            ["add", var node, .. var tail] when Add(node, tail) is { } add => Send(add),
            ["delete", var node] => Send(new ReviewContextMapRequest("delete", Node: node)),
            _ => null,
        };
        return command is null
            ? SindriErrors.UnknownVerb.With($"'review {string.Join(' ', args)}' is not a review command: {Forms}")
            : command;
    }

    private static ReviewContextMapRequest Decide(string verb, string target) =>
        target.StartsWith("edge:", StringComparison.Ordinal) ? new ReviewContextMapRequest(verb, Link: target) : new ReviewContextMapRequest(verb, Node: target);

    private static ReviewContextMapRequest? Add(string node, string[] tail)
    {
        var under = Array.IndexOf(tail, "--under");
        var words = under < 0 ? tail : tail[..under];
        var parent = under < 0 ? null : under + 1 < tail.Length ? tail[under + 1] : string.Empty;
        return words.Length == 0 || parent is { Length: 0 } || (under >= 0 && under + 2 != tail.Length)
            ? null
            : new ReviewContextMapRequest("add", Node: node, Label: string.Join(' ', words), Other: parent);
    }
}
