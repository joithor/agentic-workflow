using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>A script that passed every guard before the locks: the host, each request and CLI call resolved, and the environment CLI calls get.</summary>
/// <param name="Script">The script.</param>
/// <param name="Host">The host the requests go to; null when the script has no requests.</param>
/// <param name="Requests">The requests, in order, resolved.</param>
/// <param name="Calls">The CLI calls, in order, resolved.</param>
/// <param name="CliEnvironment">The environment every CLI call gets (LANG and the passed-through variables that are set).</param>
internal sealed record VettedRun(
    ApiScript Script, ResolvedHost? Host, IReadOnlyList<ResolvedRequest> Requests, IReadOnlyList<ResolvedCall> Calls, IReadOnlyDictionary<string, string> CliEnvironment)
{
    /// <summary>The values the run resolved that must never be recorded: every header value and every passed-through variable's value.</summary>
    internal IReadOnlyList<string> SecretValues => [.. Requests.SelectMany(static r => r.Headers.Values), .. PassedEnv.Select(name => CliEnvironment[name])];

    /// <summary>Names of the variables beyond the base ones that a CLI call receives.</summary>
    internal IReadOnlyList<string> PassedEnv => [.. CliEnvironment.Keys.Except(CliGuard.BaseEnvironment, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
