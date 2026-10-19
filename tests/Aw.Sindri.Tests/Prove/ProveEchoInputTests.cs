using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Infrastructure.Prove;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Prove;

/// <summary>
/// The test-only 'input.prove.echo' (plan decision 9, engine Q2): it serves the text of a fixed file under the prover home, logs one line
/// per call with the digest and never the text, throws on request, and exists only when AW_SINDRI_PROVE_BLOCKS=1 in a prover home.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ProveEchoInputTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-prove-echo-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class Echo(string text) : IProveEcho
    {
        public List<string> Lines { get; } = [];

        public ValueTask<string> ReadText(CancellationToken cancellationToken) => ValueTask.FromResult(text);

        public ValueTask Log(string line, CancellationToken cancellationToken)
        {
            Lines.Add(line);
            return ValueTask.CompletedTask;
        }
    }

    private static StepInputContext Context(string mode = "ok", string section = "echo") => new(
        BlockId.From(ProveEchoInput.Id), BlockId.From("step.produce"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["section"] = JsonSerializer.SerializeToElement(section), ["mode"] = JsonSerializer.SerializeToElement(mode) },
        new Dictionary<FactKey, JsonElement>());

    [Fact]
    public async Task Fill_returns_the_file_text_under_the_named_section_and_logs_the_digest_only()
    {
        var echo = new Echo("PLANT-ECHO the section text");

        var filled = await new ProveEchoInput(echo).Fill(Context(), TestContext.Current.CancellationToken);

        Assert.Equal("PLANT-ECHO the section text", filled.Value.Sections["echo"]);
        var line = Assert.Single(echo.Lines);
        Assert.Matches("^fill input\\.prove\\.echo step\\.produce [0-9a-f]{12}$", line);
        Assert.DoesNotContain("PLANT", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mode_throw_logs_the_call_then_throws()
    {
        var echo = new Echo("text");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await new ProveEchoInput(echo).Fill(Context(mode: "throw"), TestContext.Current.CancellationToken));

        _ = Assert.Single(echo.Lines);
    }

    [Fact]
    public async Task The_files_adapter_reads_echo_txt_and_appends_to_echo_log_under_the_prove_folder()
    {
        _ = Directory.CreateDirectory(Path.Combine(_root, "prove"));
        await File.WriteAllTextAsync(Path.Combine(_root, "prove", "echo.txt"), "from the file", TestContext.Current.CancellationToken);
        var files = new ProveEchoFiles(Path.Combine(_root, "prove"));

        var text = await files.ReadText(TestContext.Current.CancellationToken);
        await files.Log("fill a", TestContext.Current.CancellationToken);
        await files.Log("fill b", TestContext.Current.CancellationToken);

        Assert.Equal("from the file", text);
        Assert.Equal(["fill a", "fill b"], await File.ReadAllLinesAsync(Path.Combine(_root, "prove", "echo.log"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_missing_echo_file_fails_the_input_with_a_message_that_names_the_fix()
    {
        var files = new ProveEchoFiles(Path.Combine(_root, "prove"));

        var thrown = await Assert.ThrowsAsync<FileNotFoundException>(async () => await files.ReadText(TestContext.Current.CancellationToken));

        Assert.Contains("echo.txt", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1", true, true)]
    [InlineData(null, true, false)]
    [InlineData("0", true, false)]
    [InlineData("1", false, false)]
    public void Echo_is_present_only_with_prove_blocks_set_and_a_prover_home(string? flag, bool proverHome, bool present)
    {
        using var provider = ShippedComposition.Compose(proverHome ? Path.GetTempPath() : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aw-not-a-prover-home"), flag);

        Assert.Equal(present, provider.GetServices<BlockDescriptor>().Any(static d => d.Id.Value == ProveEchoInput.Id));
        Assert.Equal(present, provider.GetServices<StepInputRegistration>().Any(static r => r.Id.Value == ProveEchoInput.Id));
    }
}
