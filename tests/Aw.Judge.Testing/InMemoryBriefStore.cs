using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>The in-memory IBriefStore fake; it passes BriefStoreContract.</summary>
public sealed class InMemoryBriefStore : IBriefStore
{
    /// <inheritdoc />
    public ValueTask<Result<Unit>> Save(Brief brief, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryBriefStore.Save");

    /// <inheritdoc />
    public ValueTask<Result<Brief?>> ByToolUseId(ToolUseId toolUseId, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryBriefStore.ByToolUseId");

    /// <inheritdoc />
    public ValueTask<Result<Brief?>> FirstUnmappedByDispatchName(string dispatchName, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryBriefStore.FirstUnmappedByDispatchName");

    /// <inheritdoc />
    public ValueTask<Result<Brief?>> FirstUnmappedByDispatch(SessionId sessionId, PromptId promptId, string subagentType, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryBriefStore.FirstUnmappedByDispatch");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> SetAgentId(ToolUseId toolUseId, AgentId agentId, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryBriefStore.SetAgentId");
}
