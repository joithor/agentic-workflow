using System.Text;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class CursorCliProviderTests
{
    private readonly FakeProcessRunner _runner = new();

    [Fact]
    public async Task The_answer_is_the_last_json_object_in_the_result_text()
    {
        Install(Questions.ResultText("""I'll look at it. {"note":{"a":1}} Final: {"decision":"send","extra":{"x":1}}"""));

        Assert.Equal(new ProviderDecided("send", 1, "model"), await Decide(Questions.Text));
    }

    [Fact]
    public async Task It_runs_in_ask_mode_in_a_trusted_fresh_workspace_with_the_schema_in_the_prompt()
    {
        Install(Questions.ResultText("""{"decision":"send"}"""));

        _ = await Decide(Questions.Text);

        var call = Assert.Single(_runner.Calls);
        var arguments = call.Request.Arguments;
        Assert.Equal("cursor-agent", call.Request.FileName);
        Assert.Equal(CursorCliProvider.Model, ClaudeCliProviderTests.After(arguments, "--model"));
        Assert.Equal("ask", ClaudeCliProviderTests.After(arguments, "--mode"));
        Assert.Contains("--trust", arguments, StringComparer.Ordinal);
        Assert.Equal(call.Request.WorkingDirectory, ClaudeCliProviderTests.After(arguments, "--workspace"));
        Assert.Empty(Assert.IsType<IReadOnlyList<string>>(call.WorkingDirectoryFiles, exactMatch: false));
        Assert.Contains("JSON Schema", arguments[^1], StringComparison.Ordinal);
        Assert.Contains("\"enum\":[\"send\",\"batch\",\"drop\"]", arguments[^1], StringComparison.Ordinal);
        Assert.False(Directory.Exists(call.Request.WorkingDirectory));
    }

    [Fact]
    public async Task The_prompt_argument_carries_the_built_prompt_only_and_is_capped_under_the_argument_limit()
    {
        Install(Questions.ResultText("""{"decision":"send"}"""));
        var huge = Questions.Text with { Prompt = new string('p', 300_000) };

        _ = await Decide(huge);

        var prompt = Assert.Single(_runner.Calls).Request.Arguments[^1];
        Assert.DoesNotContain("hello", prompt, StringComparison.Ordinal);
        Assert.Contains("[truncated", prompt, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(prompt) < 128 * 1024, $"one argument stays under Linux's 128 KiB, got {prompt.Length} chars");
    }

    [Theory]
    [InlineData("""{"type":"result","is_error":true,"result":"Workspace Trust Required"}""", "cli-reported-error")]
    [InlineData("""{"type":"result","is_error":false,"result":"I can't tell."}""", "unparseable-answer")]
    [InlineData("""{"type":"result","is_error":false}""", "unparseable-answer")]
    [InlineData("not json", "unparseable-output")]
    public async Task A_malformed_answer_is_errored(string output, string reason)
    {
        Install(output);

        Assert.Equal(new ProviderErrored(reason), await Decide(Questions.Text));
    }

    [Fact]
    public async Task An_image_question_names_the_screenshots_by_absolute_path_in_the_evidence_workspace()
    {
        Install(Questions.ResultText("""{"decision":"ok"}"""));
        var evidence = Path.Combine(Path.GetTempPath(), "judge-evidence");

        _ = await new CursorCliProvider(_runner).Decide(Questions.Image, Questions.Evidence(evidence), Questions.Budget, TestContext.Current.CancellationToken);

        var request = Assert.Single(_runner.Calls).Request;
        Assert.Equal(evidence, request.WorkingDirectory);
        Assert.Contains(Path.Combine(evidence, "after.png") + "\n", request.Arguments[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_image_question_s_prompt_argument_is_capped_under_the_argument_limit_too()
    {
        Install(Questions.ResultText("""{"decision":"ok"}"""));
        var evidence = Path.Combine(Path.GetTempPath(), "judge-evidence");
        var huge = Questions.Image with { Prompt = new string('p', 300_000) };
        var input = Questions.Evidence(evidence);
        input["afterScreenshot"] = new string('a', 300_000) + ".png";

        _ = await new CursorCliProvider(_runner).Decide(huge, input, Questions.Budget, TestContext.Current.CancellationToken);

        var prompt = Assert.Single(_runner.Calls).Request.Arguments[^1];
        Assert.Contains("[truncated", prompt, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(prompt) < 128 * 1024, $"one argument stays under Linux's 128 KiB, got {prompt.Length} chars");
    }

    private void Install(string stdout) => _ = _runner.With("cursor-agent", new FakeProgram { StandardOutput = stdout });

    private async Task<ProviderResult> Decide(QuestionRef question) =>
        await new CursorCliProvider(_runner).Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);
}
