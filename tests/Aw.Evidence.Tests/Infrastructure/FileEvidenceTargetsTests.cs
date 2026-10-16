using Aw.Evidence.Core;
using Aw.Evidence.Infrastructure.Files;
using Aw.Evidence.Infrastructure.Processes;
using Aw.Evidence.Infrastructure.Targets;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class FileEvidenceTargetsTests : IDisposable
{
    private readonly TempDirectory _root = new();

    private static FileEvidenceTargets Targets() => new(new FileRunFiles(), TestJson.Options, new FileExecutableLocator());

    [Fact]
    public async Task Targets_invalid_json_is_AW_EVIDENCE_022_with_its_location()
    {
        _ = Directory.CreateDirectory(_root.Combine(".agentic-workflow"));
        await File.WriteAllTextAsync(_root.Combine(".agentic-workflow/evidence.json"), """{"cli":{"buildOutputs":"out"}}""", TestContext.Current.CancellationToken);

        var loaded = await Targets().LoadAsync(_root.Path, configPath: null, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.TargetsInvalid.Code, loaded.Error.Code);
        Assert.Contains("evidence.json: $.cli.buildOutputs", loaded.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_build_output_that_is_a_symlink_out_of_the_repo_is_refused()
    {
        var repo = _root.Combine("repo");
        var elsewhere = _root.Combine("elsewhere");
        _ = Directory.CreateDirectory(Path.Combine(repo, ".agentic-workflow"));
        _ = Directory.CreateDirectory(elsewhere);
        _ = File.CreateSymbolicLink(Path.Combine(repo, "out"), elsewhere);
        await File.WriteAllTextAsync(Path.Combine(repo, ".agentic-workflow", "evidence.json"), """{"cli":{"buildOutputs":["out"]}}""", TestContext.Current.CancellationToken);

        var loaded = await Targets().LoadAsync(repo, configPath: null, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.TargetsInvalid.Code, loaded.Error.Code);
        Assert.Contains("outside the repo", loaded.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_build_output_that_does_not_exist_yet_is_accepted()
    {
        var repo = _root.Combine("repo");
        _ = Directory.CreateDirectory(Path.Combine(repo, ".agentic-workflow"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".agentic-workflow", "evidence.json"), """{"cli":{"buildOutputs":["out"]}}""", TestContext.Current.CancellationToken);

        var loaded = await Targets().LoadAsync(repo, configPath: null, TestContext.Current.CancellationToken);

        Assert.Equal(["out"], loaded.Value.BuildOutputs);
    }

    public void Dispose() => _root.Dispose();
}
