using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// Where the agent's session id comes from (launcher spec §4): the hook's <c lang="text">launches/&lt;id&gt;.session-id</c>, read once and deleted, and, once the hook has had
/// its chance, the single new file the CLI wrote. An id must match the generic bound and the entry's pattern from <c lang="text">launches/&lt;id&gt;.capture.json</c>.
/// </summary>
/// <param name="home">Sindri's home.</param>
internal sealed class SessionIdFiles(string home) : ISessionIdSource
{
    internal static readonly TimeSpan HookGrace = TimeSpan.FromSeconds(10);

    public ValueTask<SessionIdScan> Look(LaunchId launch, DateTimeOffset startedAt, TimeSpan elapsed, CancellationToken cancellationToken)
    {
        _ = home;
        throw new NotSupportedException("SessionIdFiles: make SessionIdFilesTests pass");
    }
}
