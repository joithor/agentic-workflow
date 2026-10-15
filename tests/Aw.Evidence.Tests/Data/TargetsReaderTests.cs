using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class TargetsReaderTests
{
    private static Kernel.Result<ResolvedTargets> Read(string json) => TargetsReader.Read(TestJson.Utf8(json), TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void An_empty_object_allows_nothing()
    {
        var targets = Read("{}").Value;

        Assert.True(targets.AllowsNoProgram);
        Assert.Empty(targets.PassEnv);
    }

    [Fact, Trait("Lane", "unit")]
    public void Build_outputs_allow_and_pass_env_read_back_and_build_outputs_are_normalized()
    {
        var targets = Read("""{"schema":"evidence.targets/1","cli":{"buildOutputs":["out/","./build/bin"],"allow":["/opt/tool"],"passEnv":["LANG","NOTES_REGION"]}}""").Value;

        Assert.Equal(["out", "build/bin"], targets.BuildOutputs);
        Assert.Equal(["/opt/tool"], targets.Allow);
        Assert.Equal(["LANG", "NOTES_REGION"], targets.PassEnv);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_databases_object_is_accepted_for_db_run_to_read_later() =>
        Assert.True(Read("""{"databases":{"fixture":{"engine":"postgres"}}}""").IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void Targets_repo_file_cannot_hold_an_unknown_key_and_names_where()
    {
        var error = Read("""{"cli":{"buildOutputs":["out"],"allowed":[]}}""").Error;

        Assert.Equal(EvidenceErrors.TargetsInvalid.Code, error.Code);
        Assert.Contains("$.cli", error.Message, StringComparison.Ordinal);
        Assert.Contains("unknown property", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"cli":{"buildOutputs":["/abs"]}}""", "relative")]
    [InlineData("""{"cli":{"buildOutputs":[".."]}}""", "inside the repo")]
    [InlineData("""{"cli":{"buildOutputs":["a/../.."]}}""", "inside the repo")]
    [InlineData("""{"cli":{"buildOutputs":[""]}}""", "relative")]
    [InlineData("""{"cli":{"allow":["tool"]}}""", "absolute")]
    [InlineData("""{"cli":{"passEnv":["lower"]}}""", "UPPER_SNAKE")]
    [InlineData("""{"schema":"x/1"}""", "evidence.targets/1")]
    [InlineData("""{"databases":3}""", "object")]
    public void A_file_breaking_a_rule_is_refused_with_the_rule_and_its_location(string json, string rule)
    {
        var error = Read(json).Error;

        Assert.Equal(EvidenceErrors.TargetsInvalid.Code, error.Code);
        Assert.StartsWith("$", error.Message, StringComparison.Ordinal);
        Assert.Contains(rule, error.Message, StringComparison.Ordinal);
    }
}
