using System.Net;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The fake's answers for the context routes: each is a golden (or a refusal golden) chosen by what was asked, never a literal typed here.</summary>
internal static class ContextAnswers
{
    private const string Lenses1 = "map-tabs";
    private const string LensesSet = "set-two-anchors";

    /// <summary>The anchors the lens goldens hold.</summary>
    internal static IReadOnlySet<string> KnownAnchors { get; } = new HashSet<string>(
        [(string)GoldenFiles.Node("context-lenses", Lenses1)["anchor"]!["key"]!, .. GoldenFiles.Node("context-lenses", LensesSet)["anchors"]!.AsArray().Select(a => (string)a!["key"]!)],
        StringComparer.Ordinal);

    /// <summary>The lenses for a query: a refusal golden for what the goldens cannot answer, else the golden for one anchor or for a set.</summary>
    /// <param name="query">What was asked.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer Lenses(LensesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var anchors = query.Anchors.Distinct(StringComparer.Ordinal).ToList();
        if (anchors.Count > 20)
        {
            return RouteAnswer.Problem("context-lenses", "set-21-anchors-006");
        }

        if (query.MapVersion is { } pinned && !string.Equals(pinned, "v1", StringComparison.Ordinal))
        {
            return RouteAnswer.Problem("context-lenses", "map-version-not-retained-120");
        }

        if (query.Draft)
        {
            return RouteAnswer.Problem("context-lenses", "no-draft-044");
        }

        if (anchors.Exists(a => !KnownAnchors.Contains(a)))
        {
            return RouteAnswer.Problem("context-lenses", "unknown-anchor-003");
        }

        return anchors.Count switch
        {
            0 => RouteAnswer.Problem("context-lenses", "unknown-anchor-003"),
            1 when query.Depth == 1 => RouteAnswer.Api("context-lenses", "depth-1"),
            1 when string.Equals(query.MapVersion, "v1", StringComparison.Ordinal) => RouteAnswer.Api("context-lenses", "map-version-v1"),
            1 => RouteAnswer.Api("context-lenses", Lenses1),
            _ => RouteAnswer.Api("context-lenses", LensesSet),
        };
    }

    /// <summary>The file map for a query: the golden whose path matches, the unmapped one, or a refusal golden.</summary>
    /// <param name="query">What was asked.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer FileMap(FileMapQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Draft)
        {
            return RouteAnswer.Problem("context-filemap", "no-draft-044");
        }

        if (!string.Equals(query.Repo, (string)GoldenFiles.Node("context-filemap", "root")["repo"]!, StringComparison.Ordinal))
        {
            return RouteAnswer.Problem("context-filemap", "repo-outside-045");
        }

        if (query.UnmappedOnly)
        {
            return RouteAnswer.Api("context-filemap", "src-unmapped");
        }

        foreach (var scenario in (string[])["root", "src", "src-tabs"])
        {
            if (string.Equals((string)GoldenFiles.Node("context-filemap", scenario)["path"]!, query.Path ?? string.Empty, StringComparison.Ordinal))
            {
                return RouteAnswer.Api("context-filemap", scenario);
            }
        }

        return new RouteAnswer(HttpStatusCode.NotFound, "{}");
    }
}
