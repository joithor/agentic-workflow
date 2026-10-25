using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>How the stacked docs pull request of a source pull request is named and found (PB spec §7.2): by its branch and by a marker derived from the source, never by the branch alone.</summary>
internal static class DocsPulls
{
    /// <summary>The docs branch for a source head branch.</summary>
    /// <param name="sourceHead">The source pull request's head branch.</param>
    /// <returns>The branch name.</returns>
    public static string Branch(string sourceHead) => "docs/" + sourceHead;

    /// <summary>The marker the docs pull request's body carries: letters, digits, '.', '_', ':' and '-' only, so the same source is found again.</summary>
    /// <param name="source">The source pull request's repository and number.</param>
    /// <returns>The marker.</returns>
    public static string Marker((PullRepo Repo, int Number) source) =>
        "sindri.docs:" + new string([.. string.Create(CultureInfo.InvariantCulture, $"{source.Repo.Owner}-{source.Repo.Name}-{source.Number}").Select(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-')]);

    /// <summary>The repository and number a pull request link names.</summary>
    /// <param name="link">The link, 'https://host/owner/name/pull/N'.</param>
    /// <returns>The pull request, or null when the link isn't one.</returns>
    public static (PullRepo Repo, int Number)? Source(string? link)
    {
        var parts = Uri.TryCreate(link, UriKind.Absolute, out var uri) ? uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) : [];
        return uri is not null && parts.Length >= 4 && parts[^2] == "pull" && int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (new PullRepo(uri.IdnHost, parts[^4], parts[^3]), number)
            : null;
    }

    /// <summary>A text nested one level down, such as 'head.ref' of a pull request fact.</summary>
    /// <param name="fact">The fact's content.</param>
    /// <param name="outer">The object's name.</param>
    /// <param name="inner">The text's name.</param>
    /// <returns>The text, or null.</returns>
    public static string? Nested(JsonElement fact, string outer, string inner) =>
        fact.ValueKind == JsonValueKind.Object && fact.TryGetProperty(outer, out var child) && child.ValueKind == JsonValueKind.Object
        && child.TryGetProperty(inner, out var text) && text.ValueKind == JsonValueKind.String && text.GetString() is { Length: > 0 } value
            ? value
            : null;
}
