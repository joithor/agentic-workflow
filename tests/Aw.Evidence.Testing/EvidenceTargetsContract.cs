using Aw.Evidence.Core;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>
/// The IEvidenceTargets contract: a repo without a file has no targets, the repo's file or the --config file reads into targets,
/// a missing --config file is AW-EVIDENCE-010, and an invalid file is AW-EVIDENCE-022 naming the file and the JSON location.
/// </summary>
public abstract class EvidenceTargetsContract
{
    /// <summary>Arranges a repo whose evidence.json holds <paramref name="repoFile"/> (null: no file) and, when <paramref name="configFile"/> is not null, a separate config file with that content.</summary>
    /// <param name="repoFile">The repo file's text, or null for none.</param>
    /// <param name="configFile">The --config file's text, or null for no such file.</param>
    /// <returns>The targets port, the repo and the config path to give it (null when no config file was arranged).</returns>
    protected abstract Task<TargetsUnderTest> ArrangeAsync(string? repoFile, string? configFile);

    /// <summary>A repo without a file allows nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_repo_without_a_file_has_empty_targets()
    {
        var targets = await ArrangeAsync(repoFile: null, configFile: null);

        var loaded = await targets.Port.LoadAsync(targets.Repo, targets.ConfigPath, CancellationToken.None);

        Expect.That(loaded.IsSuccess && loaded.Value.AllowsNoProgram && loaded.Value.PassEnv.Count == 0, "no file means nothing is allowed");
    }

    /// <summary>The repo's file reads into targets.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_repo_file_reads_into_targets()
    {
        var targets = await ArrangeAsync("""{"schema":"evidence.repo/1","cli":{"buildOutputs":["out","build/bin"],"allow":["/opt/tool"],"passEnv":["LANG"]}}""", configFile: null);

        var loaded = await targets.Port.LoadAsync(targets.Repo, targets.ConfigPath, CancellationToken.None);

        Expect.That(
            loaded.IsSuccess && loaded.Value.BuildOutputs.SequenceEqual(["out", "build/bin"], StringComparer.Ordinal) && loaded.Value.Allow.SequenceEqual(["/opt/tool"], StringComparer.Ordinal) && loaded.Value.PassEnv.SequenceEqual(["LANG"], StringComparer.Ordinal),
            "build outputs, allow and passEnv read back");
    }

    /// <summary>The config file replaces the repo's file.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_config_file_replaces_the_repo_file()
    {
        var targets = await ArrangeAsync("""{"cli":{"buildOutputs":["from-repo"]}}""", """{"schema":"evidence.targets/1","cli":{"buildOutputs":["from-config"]}}""");

        var loaded = await targets.Port.LoadAsync(targets.Repo, targets.ConfigPath, CancellationToken.None);

        Expect.That(loaded.IsSuccess && loaded.Value.BuildOutputs.SequenceEqual(["from-config"], StringComparer.Ordinal), "only the --config file is read");
    }

    /// <summary>A --config file that is not there is AW-EVIDENCE-010.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_config_file_is_file_not_found()
    {
        var targets = await ArrangeAsync("""{"cli":{"buildOutputs":["out"]}}""", configFile: null);

        var loaded = await targets.Port.LoadAsync(targets.Repo, targets.Repo + "/absent.json", CancellationToken.None);

        Expect.That(loaded.IsError && loaded.Error.Code == EvidenceErrors.FileNotFound.Code, "a missing --config file is AW-EVIDENCE-010");
    }

    /// <summary>A file breaking a rule is AW-EVIDENCE-022 naming the file and the location.</summary>
    /// <param name="file">The file's text.</param>
    /// <param name="location">A location the message must name.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("not json", "$")]
    [InlineData("""{"cli":{"buldOutputs":["out"]}}""", "$.cli")]
    [InlineData("""{"cli":{"buildOutputs":["/abs/out"]}}""", "$.cli.buildOutputs[0]")]
    [InlineData("""{"cli":{"buildOutputs":["../elsewhere"]}}""", "$.cli.buildOutputs[0]")]
    [InlineData("""{"cli":{"buildOutputs":["out/../../elsewhere"]}}""", "$.cli.buildOutputs[0]")]
    [InlineData("""{"cli":{"allow":["relative/tool"]}}""", "$.cli.allow[0]")]
    [InlineData("""{"cli":{"passEnv":["lower"]}}""", "$.cli.passEnv[0]")]
    [InlineData("""{"schema":"other/1"}""", "$.schema")]
    [InlineData("""{"databases":[]}""", "$.databases")]
    public async Task An_invalid_file_is_AW_EVIDENCE_022_naming_the_file_and_the_location(string file, string location)
    {
        var targets = await ArrangeAsync(file, configFile: null);

        var loaded = await targets.Port.LoadAsync(targets.Repo, targets.ConfigPath, CancellationToken.None);

        Expect.That(loaded.IsError && loaded.Error.Code == EvidenceErrors.TargetsInvalid.Code, "an invalid file is AW-EVIDENCE-022");
        Expect.That(loaded.Error.Message.Contains("evidence.json", StringComparison.Ordinal) && loaded.Error.Message.Contains(location, StringComparison.Ordinal), $"the message names the file and {location}");
    }
}
