using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;

namespace Aw.Evidence.Tests.Desktop;

public sealed class DesktopManifestTests
{
    private const string Head = "\"schema\":\"ui-evidence.desktop-screens/1\",\"designRoot\":\"mockups\"";

    private static string Manifest(string screens, string head = Head) => "{" + head + ",\"screens\":[" + screens + "]}";

    private const string Home = """{"name":"home","route":"page:Home","design":"home.html"}""";

    private static Aw.Kernel.Result<DesktopManifest> Read(string json) => DesktopManifestReader.Read(TestJson.Utf8(json), TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void A_manifest_with_a_screen_reads_and_keeps_every_field()
    {
        var read = Read(Manifest("""
            {"name":"workflow-project","route":"workflow/PRJ-sync","design":"workflow-project.html","threshold":12.5,"settleMs":1500,
             "fixture":[["workflows"],["items"]],"sizes":["1440x900"],"accepted":["clock text differs"],
             "masks":[{"x":10,"y":20,"w":30,"h":40,"why":"relative times","size":"1440x900"}]}
            """));

        var screen = Assert.Single(read.Value.Screens);
        Assert.Equal(("workflow-project", "workflow/PRJ-sync", "workflow-project.html", 12.5, 1500), (screen.Name, screen.Route, screen.Design, screen.Threshold, screen.SettleMs));
        Assert.Equal(["workflows", "items"], screen.Fixture!.Select(static step => string.Join(' ', step)));
        Assert.Equal(new ScreenMask(10, 20, 30, 40, "relative times", "1440x900"), Assert.Single(screen.Masks!));
        Assert.Equal(["clock text differs"], screen.Accepted);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"name":"Home","route":"page:Home","design":"home.html"}""", "name: lowercase letters")]
    [InlineData("""{"name":"home","route":"","design":"home.html"}""", "route: must not be empty")]
    [InlineData("""{"name":"home","route":"page:Home","design":"../home.html"}""", "design: must be an .html file below the design root")]
    [InlineData("""{"name":"home","route":"page:Home","design":"home.png"}""", "design: must be an .html file")]
    [InlineData("""{"name":"home","route":"page:Home","design":"home.html","threshold":101}""", "threshold: must be 0 to 100")]
    [InlineData("""{"name":"home","route":"page:Home","design":"home.html","sizes":["wide"]}""", "'wide' is not a size")]
    [InlineData("""{"name":"home","route":"page:Home","design":"home.html","masks":[{"x":0,"y":0,"w":0,"h":4,"why":"x"}]}""", "a mask needs x, y of 0 or more and w, h of 1 or more")]
    [InlineData("""{"name":"home","route":"page:Home","design":"home.html","masks":[{"x":0,"y":0,"w":4,"h":4,"why":" "}]}""", "a mask needs a 'why'")]
    [InlineData("""{"name":"home","route":"page:Home","design":"home.html","fixture":[[]]}""", "each step must list its arguments")]
    public void A_screen_that_breaks_a_rule_is_refused_with_the_rule_named(string screen, string expected)
    {
        var read = Read(Manifest(screen));

        Assert.Equal(EvidenceErrors.DesktopManifestInvalid.Code, read.Error.Code);
        Assert.Contains(expected, read.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_screens_with_one_name_are_refused()
    {
        var read = Read(Manifest(Home + "," + Home));

        Assert.Contains("the name 'home' is used twice", read.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_manifest_with_the_wrong_schema_no_screens_or_an_unknown_key_is_refused()
    {
        Assert.Contains("schema: must be ui-evidence.desktop-screens/1", Read(Manifest(Home, "\"schema\":\"x\",\"designRoot\":\".\"")).Error.Message, StringComparison.Ordinal);
        Assert.Contains("screens: must list at least one screen", Read(Manifest(string.Empty)).Error.Message, StringComparison.Ordinal);
        Assert.Contains("extra", Read(Manifest(Home, Head + ",\"extra\":1")).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_design_root_may_be_the_manifests_own_folder_or_another_relative_one_but_never_absolute()
    {
        Assert.True(Read(Manifest(Home, "\"schema\":\"ui-evidence.desktop-screens/1\",\"designRoot\":\".\"")).IsSuccess);
        Assert.Contains("designRoot", Read(Manifest(Home, "\"schema\":\"ui-evidence.desktop-screens/1\",\"designRoot\":\"/x\"")).Error.Message, StringComparison.Ordinal);
    }
}
