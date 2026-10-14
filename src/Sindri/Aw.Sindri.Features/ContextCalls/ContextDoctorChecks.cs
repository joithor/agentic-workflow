using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Doctor's context checks. context:sources asks the engine in-process with the approved profile from the same home
/// serve reads (so it works with the process stopped); context:mcp-scope reads the three providers' user-wide MCP files.
/// </summary>
internal sealed class ContextDoctorChecks(ContextScopes scopes, IContextEngine engine, IProviderFiles files) : IDoctorChecks
{
    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        var sources = scope.IsError
            ? ContextChecks.NoProfile(scope.Error)
            : scope.Value.Settings is null
                ? ContextChecks.NoSection()
                : ContextChecks.Sources(await engine.Sources(scope.Value, cancellationToken).ConfigureAwait(false));
        var findings = new List<McpScopeFinding>();
        foreach (var file in Enum.GetValues<ProviderFile>())
        {
            if (McpScope.Judge(await files.Read(file, cancellationToken).ConfigureAwait(false)) is { } finding)
            {
                findings.Add(finding);
            }
        }

        return [sources, ContextChecks.McpScope(findings)];
    }
}
