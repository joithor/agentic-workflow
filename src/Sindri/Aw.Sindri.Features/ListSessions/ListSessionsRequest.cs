using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.ListSessions;

/// <summary>List the live agent sessions (GET /sindri/sessions?provider=&amp;state=&amp;repo=&amp;inWorkflow=; collector spec CA-1).</summary>
/// <param name="Provider">Only this agent's sessions, or null.</param>
/// <param name="State">Only sessions in this state, or null.</param>
/// <param name="Repo">Only sessions in this repo, or null.</param>
/// <param name="InWorkflow">True: only launched sessions; false: only the ones outside a workflow; null: both.</param>
public sealed record ListSessionsRequest(AgentId? Provider = null, SessionState? State = null, RepoName? Repo = null, bool? InWorkflow = null) : IQuery<SessionList>;
