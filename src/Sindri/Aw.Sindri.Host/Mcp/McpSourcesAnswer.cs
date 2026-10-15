using Aw.Context.Core.Results;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// context_sources' structuredContent: the sources view's fields plus which instance answered. Claude Code hands the model
/// structuredContent rather than the text block when both are present, so the build and port live here too.
/// </summary>
/// <param name="Instance">The Sindri build and port that answered.</param>
/// <param name="Sources">One row per source and repo.</param>
/// <param name="SessionsEnabled">Whether session reads are on.</param>
/// <param name="AsOf">When the view was assembled.</param>
internal sealed record McpSourcesAnswer(McpInstance Instance, IReadOnlyList<SourceStatusRow> Sources, bool SessionsEnabled, DateTimeOffset AsOf)
{
    /// <summary>The answer for <paramref name="view"/> from this instance.</summary>
    internal static McpSourcesAnswer Of(SourcesView view, string build, int port) =>
        new(new McpInstance(build, port), view.Sources, view.SessionsEnabled, view.AsOf);
}
