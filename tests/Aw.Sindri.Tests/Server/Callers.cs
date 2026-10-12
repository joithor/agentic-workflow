using Aw.Sindri.Core.Auth;
using Aw.Sindri.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>Caller headers for API tests (API spec §6): a human with the running host's per-start token, or a session.</summary>
public static class Callers
{
    /// <summary>Marks <paramref name="request"/> as the human CLI, with the token <paramref name="app"/> holds (none when it holds none).</summary>
    /// <param name="request">The request.</param>
    /// <param name="app">The running host.</param>
    /// <param name="terminal">Whether to claim an interactive terminal.</param>
    /// <returns>The same request.</returns>
    public static HttpRequestMessage AsHuman(this HttpRequestMessage request, WebApplication app, bool terminal = true)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(app);
        _ = request.Headers.TryAddWithoutValidation(SindriHeaders.Caller, "human");
        _ = request.Headers.TryAddWithoutValidation(SindriHeaders.Client, "cli");
        if (terminal)
        {
            _ = request.Headers.TryAddWithoutValidation(SindriHeaders.Terminal, "1");
        }

        if (app.Services.GetRequiredService<HeldHumanToken>().Current is { } token)
        {
            _ = request.Headers.TryAddWithoutValidation(SindriHeaders.Token, token.Value);
        }

        return request;
    }

    /// <summary>Marks <paramref name="request"/> as a session (the CLI inside an agent session); <paramref name="repo"/> scopes it.</summary>
    /// <param name="request">The request.</param>
    /// <param name="repo">The repo name, or null.</param>
    /// <returns>The same request.</returns>
    public static HttpRequestMessage AsSession(this HttpRequestMessage request, string? repo = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.Headers.TryAddWithoutValidation(SindriHeaders.Caller, "session");
        _ = request.Headers.TryAddWithoutValidation(SindriHeaders.Client, "cli");
        if (repo is not null)
        {
            _ = request.Headers.TryAddWithoutValidation(SindriHeaders.Repo, repo);
        }

        return request;
    }
}
