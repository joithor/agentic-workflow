using Aw.Evidence.Core;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Kernel;

namespace Aw.Evidence.Tests.Design;

public sealed class ManifestReaderTests
{
    private const string Frame = """{"name":"empty-state","designPng":"/d/empty.png","designRegion":{"x":0,"y":0,"w":10,"h":10},"anchor":{"testId":"card"}}""";

    private static string Manifest(string extra = "", string frame = Frame) =>
        """{"route":"/hospitals","ready":{"text":"Hospitals"},"frames":[""" + frame + "]" + extra + "}";

    private static Result<DesignManifest> Read(string json) => ManifestReader.Read(TestJson.Utf8(json), TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void A_minimal_manifest_reads_with_its_defaults()
    {
        var m = Read(Manifest()).Value;

        Assert.Equal(new ViewportSize(1512, 982), m.Viewport);
        Assert.Equal(60_000, m.TimeoutMs);
        Assert.Equal(600, m.SettleMs);
        Assert.Equal(0.1, m.Threshold);
        Assert.False(m.IncludeAA);
        Assert.Equal(new Offset(1490, 960), m.NeutralClickPoint());
        Assert.Empty(m.Frames[0].Steps);
        Assert.Equal("testId:card", m.Frames[0].Anchor.Key);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_neutral_click_can_be_moved_or_skipped()
    {
        Assert.Equal(new Offset(5, 6), Read(Manifest(""","neutralClick":{"x":5,"y":6}""")).Value.NeutralClickPoint());
        Assert.Null(Read(Manifest(""","skipNeutralClick":true""")).Value.NeutralClickPoint());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_login_block_defaults_its_login_path()
    {
        var m = Read(Manifest(""","login":{"emailEnv":"APP_EMAIL","passwordEnv":"APP_PASSWORD","emailSelector":"#e","passwordSelector":"#p","submitSelector":"#s"}""")).Value;

        Assert.Equal("/login", m.Login?.LoginPath);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(""","expectBox":[]""")]
    [InlineData(""","viewport":{"w":10,"h":10,"d":2}""")]
    public void A_misspelt_key_at_any_depth_is_refused(string extra) =>
        Assert.Equal(EvidenceErrors.ManifestInvalid.Code, Read(Manifest(extra)).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_misspelt_key_is_named_by_its_json_location()
    {
        var frame = Frame.Replace("}}", ""","zoom":2}""", StringComparison.Ordinal);

        Assert.Equal("$.frames[0].zoom: unknown property", Read(Manifest(frame: frame)).Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_misspelt_key_inside_a_frame_is_refused()
    {
        var frame = Frame.Replace("}}", """},"expectBox":[{"target":{"testId":"a"},"expected":{"w":1}}]}""", StringComparison.Ordinal);

        Assert.Equal(EvidenceErrors.ManifestInvalid.Code, Read(Manifest(frame: frame)).Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"route":"hospitals","ready":{"text":"H"},"frames":[F]}""", "route must start with '/'")]
    [InlineData("""{"route":"/","ready":{"text":"H","testId":"x"},"frames":[F]}""", "ready needs exactly one")]
    [InlineData("""{"route":"/","ready":{"text":"H"},"frames":[]}""", "at least one frame")]
    [InlineData("""{"route":"/","ready":{"text":"H"},"frames":[F,F]}""", "duplicate frame name \"empty-state\"")]
    [InlineData("""{"route":"/","ready":{"text":"H"},"threshold":1.5,"frames":[F]}""", "threshold must be between 0 and 1")]
    [InlineData("""{"route":"/","ready":{"text":"H"},"timeoutMs":0,"frames":[F]}""", "timeoutMs must be positive")]
    [InlineData("""{"route":"/","ready":{"text":"H"},"login":{"emailEnv":"email","passwordEnv":"P","emailSelector":"#e","passwordSelector":"#p","submitSelector":"#s"},"frames":[F]}""", "UPPER_SNAKE")]
    public void A_manifest_breaking_a_rule_names_the_rule(string json, string rule)
    {
        var result = Read(json.Replace("F", Frame, StringComparison.Ordinal));

        Assert.Equal(EvidenceErrors.ManifestInvalid.Code, result.Error.Code);
        Assert.Contains(rule, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"name\":\"empty-state\"", "\"name\":\"-bad\"", "name must be letters")]
    [InlineData("\"w\":10", "\"w\":0", "w,h > 0")]
    [InlineData("\"anchor\":{\"testId\":\"card\"}", "\"anchor\":{}", "anchor needs exactly one")]
    [InlineData("}}", "},\"expectBoxes\":[{\"target\":{\"testId\":\"a\"},\"expected\":{}}]}", "at least one of w, h, x and y")]
    [InlineData("}}", "},\"expectBoxes\":[{\"target\":{\"testId\":\"a\"},\"expected\":{\"w\":1},\"tolerance\":-1}]}", "tolerance must not be negative")]
    [InlineData("}}", "},\"formValues\":[{\"value\":\"x\"}]}", "formValue needs exactly one")]
    [InlineData("}}", "},\"knownDifferences\":[\"\"]}", "empty entry")]
    [InlineData("}}", "},\"steps\":[{\"action\":\"goto\",\"target\":\"/\",\"expectedState\":{\"kind\":\"text-visible\",\"text\":\"\"}}]}", "text-visible needs a text")]
    public void A_frame_breaking_a_rule_names_the_rule(string find, string replace, string rule)
    {
        var result = Read(Manifest(frame: Frame.Replace(find, replace, StringComparison.Ordinal)));

        Assert.Equal(EvidenceErrors.ManifestInvalid.Code, result.Error.Code);
        Assert.Contains(rule, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Frame_extras_read_into_typed_values()
    {
        var frame = Frame.Replace("}}", """},"anchorOffset":{"x":-4,"y":2.5},"waitFor":{"text":"Loaded"},"formValues":[{"css":"#q","value":"St"}],"expectBoxes":[{"target":{"testId":"a"},"relativeTo":{"text":"Head"},"expected":{"w":40},"tolerance":1}],"knownDifferences":["icon set"],"designNode":"1:2"}""", StringComparison.Ordinal);

        var f = Read(Manifest(frame: frame)).Value.Frames[0];

        Assert.Equal(new Offset(-4, 2.5), f.AnchorOffset);
        Assert.Equal("text:Loaded", f.WaitFor?.Key);
        Assert.Equal("#q", f.FormValues[0].Css);
        Assert.Equal("text:Head", f.ExpectBoxes[0].RelativeTo?.Key);
        Assert.Equal(1, f.ExpectBoxes[0].Tolerance);
        Assert.Equal(["icon set"], f.KnownDifferences);
    }
}
