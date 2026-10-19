using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>Plain text for the map verbs: words, never colour, so NO_COLOR and pipes read the same.</summary>
internal static class ContextMapText
{
    internal static string Counts(MapCounts c) => string.Create(CultureInfo.InvariantCulture,
        $"{c.Nodes} nodes, {c.Edges} edges, {c.Aliases} aliases, {c.Decisions} decisions, {c.Overrides} overrides");

    internal static string Preview(MapFilePreview p) => $"map file {p.Path}\nrepo {p.Repo.Value}, {Kind(p.Kind)}: {Counts(p.Counts)}\nhash {p.Hash}\n";

    internal static string Imported(ImportContextMapResponse r) => r.Imported is not { } i
        ? Preview(r.Preview)
        : string.Create(CultureInfo.InvariantCulture,
            $"Imported map v{i.Version.Number} (current) for {i.Version.Repo.Value}: {Counts(i.Counts)}\nhash {i.Hash}; recorded as event {r.Sequence}\n");

    internal static string Status(MapStatusView s) => string.Create(CultureInfo.InvariantCulture,
        $"map v{s.Version.Number} {Word(s.Version.Status)} · {s.Version.Repo.Value} · created {s.Version.Created:O}{(s.ScopedTo is { } repo ? $" · scoped to {repo.Value}" : string.Empty)}\n"
        + $"{Counts(s.Counts)}{Scope(s.ScopedTo)}; {s.Orphaned} orphaned overrides; {s.Retained} retained versions\n")
        + (s.Coverage is { } coverage ? ContextLensText.Coverage(coverage) : string.Empty);

    internal static string Exported(MapExported e) => string.Create(CultureInfo.InvariantCulture,
        $"Exported map v{e.Version} to {e.Path}: {Counts(e.Counts)}\n");

    // A session's counts are its repo's slice (spec §3), so they say so: doctor's are the whole map's, and the two lines are read side by side.
    private static string Scope(ContextRepoName? repo) => repo is { } r
        ? $" in {r.Value}'s scope (the nodes {r.Value} implements and the intent linked to them; doctor counts the whole map)"
        : string.Empty;

    private static string Kind(MapDocumentKind kind) => kind == MapDocumentKind.Export ? "export" : "authored";

    private static string Word(MapVersionStatus status) => status switch
    {
        MapVersionStatus.Current => "current",
        MapVersionStatus.Retained => "retained",
        MapVersionStatus.Draft => "draft",
        MapVersionStatus.Building => "building",
        _ => "unknown",
    };
}
