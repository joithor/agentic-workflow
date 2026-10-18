using System.Globalization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Mcp;

/// <summary>
/// The one registration Sindri makes (context spec §3, API spec §8): Claude Code's local scope, inside the work repo's
/// checkout, with the repo header the endpoint scopes by. Codex and Cursor are never registered; the launcher passes
/// them the endpoint at launch time (API spec §8.1).
/// </summary>
public static class McpRegistration
{
    /// <summary>The claude CLI's arguments for <paramref name="repo"/> on <paramref name="port"/>.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="port">Sindri's API port.</param>
    /// <returns>The arguments after 'claude'; the header goes last, since claude's '--header &lt;header...&gt;' takes every later argument.</returns>
    public static IReadOnlyList<string> Arguments(RepoName repo, int port) =>
        ["mcp", "add", "--transport", "http", "--scope", "local", McpScope.ServerName,
            string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/sindri/mcp"), "--header", "X-Sindri-Repo: " + repo.Value];

    /// <summary>The port to register: the running instance's, else the profile's API port.</summary>
    /// <param name="running">The running instance's URL, or null when none is running.</param>
    /// <param name="profilePort">The approved profile's api.port, or null.</param>
    /// <returns>The port.</returns>
    public static int Port(Uri? running, int? profilePort) =>
        running is { IsDefaultPort: false } url ? url.Port : profilePort ?? Profile.ApiSettings.DefaultPort;
}
