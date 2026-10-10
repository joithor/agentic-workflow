using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>IBriefStore over decisions.sqlite (the briefs table).</summary>
internal sealed class SqliteBriefStore : IBriefStore
{
    public ValueTask<Result<Unit>> Save(Brief brief, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteBriefStore.Save");

    public ValueTask<Result<Brief?>> ByToolUseId(ToolUseId toolUseId, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteBriefStore.ByToolUseId");

    public ValueTask<Result<Brief?>> FirstUnmappedByDispatchName(string dispatchName, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteBriefStore.FirstUnmappedByDispatchName");

    public ValueTask<Result<Brief?>> FirstUnmappedByDispatch(SessionId sessionId, PromptId promptId, string subagentType, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteBriefStore.FirstUnmappedByDispatch");

    public ValueTask<Result<Unit>> SetAgentId(ToolUseId toolUseId, AgentId agentId, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteBriefStore.SetAgentId");
}
