using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>A session is live only when its process is the same process (collector spec CO5).</summary>
public sealed class SessionLivenessTests
{
    [Fact, Trait("Lane", "unit")]
    public void SessionLiveness_rejects_a_recycled_pid() =>
        Assert.False(SessionLiveness.Matches(procStart: "1100", actualTicks: 9000));

    [Fact, Trait("Lane", "unit")]
    public void SessionLiveness_reads_procStart_as_a_string()
    {
        Assert.True(SessionLiveness.Matches(procStart: "13279367", actualTicks: 13279367));
        Assert.False(SessionLiveness.Matches(procStart: null, actualTicks: 1));
        Assert.False(SessionLiveness.Matches(procStart: "not a number", actualTicks: 1));
        Assert.False(SessionLiveness.Matches(procStart: "", actualTicks: 0));
    }
}
