using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Workflows;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The reason a Step fails with when its agent's session ended without a report.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentEndReasonsTests
{
    [Theory]
    [InlineData(AgentSessionEnd.Exit, "exit")]
    [InlineData(AgentSessionEnd.Lost, "lost")]
    [InlineData(AgentSessionEnd.Idle, "idle")]
    public void Each_way_a_session_ends_has_its_own_reason(AgentSessionEnd ended, string reason) => Assert.Equal(reason, AgentEndReasons.Of(ended));
}
