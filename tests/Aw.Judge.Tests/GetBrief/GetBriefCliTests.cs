namespace Aw.Judge.Tests.GetBrief;

// The frozen surface of 'judge brief get' (TS runBriefGet: the BriefRow as JSON, savedAt as toISOString).
public sealed class GetBriefCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Brief_get_prints_the_saved_brief_in_the_TS_key_order()
    {
        var cli = new JudgeCli(_state);
        cli.Clock.Advance(TimeSpan.FromMilliseconds(1234.5678));
        _ = await cli.RunWithInput(
            """{"toolUseId":"toolu_1","sessionId":"s1","promptId":"p1","dispatchName":"helper","subagentType":"Explore","goal":"Find \"it\"","acceptanceCriteria":"é\n😀","proofCommand":""}""",
            "brief", "save");

        var run = await cli.Run("brief", "get", "toolu_1");

        Assert.Equal(new CliRun(0,
            """{"toolUseId":"toolu_1","agentId":null,"sessionId":"s1","promptId":"p1","dispatchName":"helper","subagentType":"Explore","goal":"Find \"it\"","acceptanceCriteria":"é\n😀","proofCommand":"","savedAt":"2026-01-01T00:00:01.234Z"}""" + "\n",
            ""), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Brief_get_exits_1_when_no_brief_was_saved()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(1, "", "no brief for tool_use_id: toolu_none\n"), await cli.Run("brief", "get", "toolu_none"));
        Assert.Equal(new CliRun(1, "", "no brief for tool_use_id: \n"), await cli.Run("brief", "get"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_brief_subcommand_prints_the_brief_usage()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(1, "", "usage: judge brief save|get|map-by-name|map-by-dispatch|set-agent-id ...\n"), await cli.Run("brief", "list"));
        Assert.Equal(new CliRun(1, "", "usage: judge brief save|get|map-by-name|map-by-dispatch|set-agent-id ...\n"), await cli.Run("brief"));
    }

    public void Dispose() => _state.Dispose();
}
