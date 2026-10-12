using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>
/// The context engine's one facade (spec §4): every surface (HTTP, CLI, MCP, scheduled services, doctor) calls it with
/// Core records only. Sindri passes the approved profile's 'context:' section, its repos and the caller on every call;
/// the engine keeps no profile of its own.
/// </summary>
public interface IContextEngine : IContextMap
{
    /// <summary>Validates a profile's 'context:' section against the profile's repos and applies its defaults.</summary>
    /// <param name="section">The section, or null when the profile has none.</param>
    /// <param name="repos">The profile's repos as written: with a section, 'context.repos' may name only these, and every repo in scope needs a name the engine can use.</param>
    /// <returns>The settings, or SND-CONTEXT-001 naming the key path ('context.deadline: …', 'context.repos: …').</returns>
    public Result<ContextSettings> CheckSettings(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos);

    /// <summary>Answers one question (spec §8): anchors, candidates, fusion, stale check, packing, citations.</summary>
    /// <param name="query">The question.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The cited result (possibly degraded or empty), or a catalogue error (§8.6).</returns>
    public ValueTask<Result<ContextResult>> Query(ContextQuery query, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Each configured source's state (spec §8.5 'context sources status').</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The sources view.</returns>
    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Brings the snapshot sources (repo-docs' index) up to date with each repo's HEAD.</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    /// <returns>What changed per repo.</returns>
    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken);
}
