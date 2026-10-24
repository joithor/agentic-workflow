using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Desktop;

public sealed class DesktopTargetsTests
{
    private const string Fixture = "\"fixture\":{\"command\":[\"scripts/fx.sh\"],\"up\":[[\"up\"],[\"items\",\"--large\"]],\"down\":[\"down\"]}";
    private const string Capture = "\"capture\":{\"command\":[\"cap\",\"--size\",\"{size}\",\"{shots}\"]}";

    private static string Config(string desktop) => "{\"desktop\":{" + desktop + "}}";

    private static Aw.Kernel.Result<ResolvedTargets> Read(string json) => TargetsReader.Read(TestJson.Utf8(json), TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void A_desktop_part_reads_with_its_defaults_filled_in()
    {
        var desktop = Read(Config($"\"manifest\":\"docs/screens.json\",\"homeEnv\":\"APP_HOME\",{Fixture},{Capture}")).Value.Desktop!;

        Assert.Equal(("docs/screens.json", "APP_HOME", 10d, TimeSpan.FromSeconds(300)), (desktop.Manifest, desktop.HomeEnv, desktop.Threshold, desktop.Timeout));
        Assert.Equal(["1440x900", "900x900"], desktop.Sizes.Select(static s => s.Describe()));
        Assert.Equal(["up", "items --large"], desktop.Fixture.Up.Select(static step => string.Join(' ', step)));
        Assert.Equal(["down"], desktop.Fixture.Down);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_sizes_the_threshold_the_timeout_and_the_env_are_taken_as_given()
    {
        var desktop = Read(Config($"\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",{Fixture},{Capture},\"sizes\":[\"1200x800\"],\"threshold\":5,\"timeoutSeconds\":60,\"env\":{{\"APP_CLI\":\"bin/app\"}}")).Value.Desktop!;

        Assert.Equal(("1200x800", 5d, TimeSpan.FromSeconds(60), "bin/app"), (Assert.Single(desktop.Sizes).Describe(), desktop.Threshold, desktop.Timeout, desktop.Env["APP_CLI"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_config_without_a_desktop_part_has_none()
    {
        Assert.Null(Read("{}").Value.Desktop);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"manifest\":\"/abs.json\",\"homeEnv\":\"APP_HOME\",FX,CAP", "$.desktop.manifest: must be a file path below the repo")]
    [InlineData("\"manifest\":\"../s.json\",\"homeEnv\":\"APP_HOME\",FX,CAP", "$.desktop.manifest")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"home\",FX,CAP", "$.desktop.homeEnv: must be an UPPER_SNAKE")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,\"capture\":{\"command\":[\"cap\",\"{size}\"]}", "must hold the element {shots}")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,\"capture\":{\"command\":[\"cap\",\"{shots}\"]}", "must hold {size}")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",\"fixture\":{\"command\":[\"fx\"],\"up\":[]},CAP", "$.desktop.fixture.up: must list at least one step")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,CAP,\"sizes\":[\"big\"]", "'big' is not a size")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,CAP,\"threshold\":120", "$.desktop.threshold: must be 0 to 100")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,CAP,\"timeoutSeconds\":0", "$.desktop.timeoutSeconds")]
    [InlineData("\"manifest\":\"s.json\",\"homeEnv\":\"APP_HOME\",FX,CAP,\"env\":{\"lower\":\"x\"}", "$.desktop.env.lower: the name must be UPPER_SNAKE")]
    public void A_desktop_part_that_breaks_a_rule_is_refused_with_the_rule_named(string desktop, string expected)
    {
        var read = Read(Config(desktop.Replace("FX", Fixture, StringComparison.Ordinal).Replace("CAP", Capture, StringComparison.Ordinal)));

        Assert.Equal(EvidenceErrors.TargetsInvalid.Code, read.Error.Code);
        Assert.Contains(expected, read.Error.Message, StringComparison.Ordinal);
    }
}
