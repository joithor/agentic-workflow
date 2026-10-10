using Aw.Judge.Core.Ids;
using Aw.Judge.Infrastructure.Sqlite;

namespace Aw.Judge.Tests.SaveBrief;

// The frozen surface of 'judge brief save' (TS cli.ts + commands.ts runBriefSave: a zod schema of eight required
// fields, every one a string — empty is fine — and dispatchName a string or null).
public sealed class SaveBriefCliTests : IDisposable
{
    private const string Payload =
        """{"toolUseId":"toolu_9","sessionId":"","promptId":"p","dispatchName":null,"subagentType":"Explore","goal":"","acceptanceCriteria":"a","proofCommand":"c","extra":1}""";

    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Brief_save_stores_the_payload_and_prints_nothing()
    {
        var run = await new JudgeCli(_state).RunWithInput(Payload, "brief", "save");

        Assert.Equal(new CliRun(0, "", ""), run);
        var saved = await new SqliteBriefStore(_state.Database()).ByToolUseId(ToolUseId.From("toolu_9"), CancellationToken.None);
        Assert.NotNull(saved.Value);
        Assert.Equal(("", "Explore", "", null), (saved.Value.SessionId.Value, saved.Value.SubagentType, saved.Value.Goal, saved.Value.DispatchName));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("")]
    [InlineData("{\"toolUseId\":")]
    [InlineData("﻿{}")]
    public async Task Brief_save_refuses_stdin_that_isnt_JSON(string stdin) =>
        Assert.Equal(new CliRun(1, "", "input is not valid JSON\n"), await new JudgeCli(_state).RunWithInput(stdin, "brief", "save"));

    [Theory, Trait("Lane", "integration")]
    [InlineData("""{"toolUseId":"t","sessionId":"s","promptId":"p","dispatchName":null,"subagentType":"x","goal":"g","acceptanceCriteria":"a"}""")]
    [InlineData("""{"toolUseId":"t","sessionId":"s","promptId":"p","subagentType":"x","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""")]
    [InlineData("""{"toolUseId":"t","sessionId":"s","promptId":"p","dispatchName":7,"subagentType":"x","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""")]
    [InlineData("""{"toolUseId":1,"sessionId":"s","promptId":"p","dispatchName":null,"subagentType":"x","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"brief\"")]
    public async Task Brief_save_refuses_a_payload_that_misses_the_schema(string stdin) =>
        Assert.Equal(new CliRun(1, "", "invalid brief payload\n"), await new JudgeCli(_state).RunWithInput(stdin, "brief", "save"));

    [Fact, Trait("Lane", "integration")]
    public async Task Brief_save_exits_1_when_the_tool_use_id_already_has_a_brief()
    {
        var cli = new JudgeCli(_state);
        _ = await cli.RunWithInput(Payload, "brief", "save");

        var run = await cli.RunWithInput(Payload, "brief", "save");

        Assert.Equal(new CliRun(1, "", "brief already saved: toolu_9\n"), run);
    }

    public void Dispose() => _state.Dispose();
}
