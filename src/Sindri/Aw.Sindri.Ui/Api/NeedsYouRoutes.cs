using System.Text;

namespace Aw.Sindri.Ui.Api;

/// <summary>The routes of the Needs you reads (API P4), with their query strings: only the filters asked for are sent.</summary>
internal static class NeedsYouRoutes
{
    internal const string NeedsYou = "/sindri/needs-you";
    internal const string Requests = "/sindri/requests";

    /// <summary>GET /sindri/decisions with its filters.</summary>
    /// <param name="pending">True, false, or null for no filter.</param>
    /// <param name="item">An item id, or null.</param>
    /// <returns>The path and query.</returns>
    internal static string Decisions(bool? pending, string? item) =>
        With("/sindri/decisions", ("pending", pending is { } p ? (p ? "true" : "false") : null), ("item", item));

    /// <summary>GET /sindri/requests with its filters.</summary>
    /// <param name="state">A request state, or null.</param>
    /// <param name="item">An item id, or null.</param>
    /// <returns>The path and query.</returns>
    internal static string RequestList(string? state, string? item) => With(Requests, ("state", state), ("item", item));

    /// <summary>POST /sindri/decisions/{id}.</summary>
    /// <param name="decisionId">The decision.</param>
    /// <returns>The path.</returns>
    internal static string Decide(string decisionId) => "/sindri/decisions/" + Uri.EscapeDataString(decisionId);

    private static string With(string path, params (string Name, string? Value)[] filters)
    {
        var query = new StringBuilder();
        foreach (var (name, value) in filters)
        {
            if (value is not null)
            {
                _ = query.Append(query.Length == 0 ? '?' : '&').Append(name).Append('=').Append(Uri.EscapeDataString(value));
            }
        }

        return path + query;
    }
}
