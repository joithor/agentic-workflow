using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class OnyxBindingsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public void Only_nginx_on_loopback_at_the_chosen_port_passes()
        => Assert.Null(OnyxBindings.Verify([new ComposePublishedPort("nginx", "127.0.0.1", 3001, 80)], 3001));

    [Theory, Trait("Lane", "unit")]
    [InlineData("nginx", "0.0.0.0", 3000, 80)]
    [InlineData("nginx", "", 3000, 80)]
    [InlineData("nginx", "127.0.0.1", 3005, 80)]
    [InlineData("api_server", "127.0.0.1", 8080, 8080)]
    [InlineData("relational_db", "0.0.0.0", 5432, 5432)]
    public void Any_other_publish_is_refused_naming_the_service_and_address(string service, string ip, int published, int target)
    {
        var error = OnyxBindings.Verify([new ComposePublishedPort("nginx", "127.0.0.1", 3000, 80), new ComposePublishedPort(service, ip, published, target)], 3000);

        Assert.Equal("SND-ONYX-004", error!.Code);
        Assert.Contains("only binds Onyx to loopback", error.Message, StringComparison.Ordinal);
        Assert.Contains(service, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_compose_that_publishes_nothing_is_refused_because_nginx_is_how_onyx_is_reached()
        => Assert.Equal("SND-ONYX-004", OnyxBindings.Verify([], 3000)!.Code);

    [Fact, Trait("Lane", "unit")]
    public async Task Up_refuses_a_rendered_compose_that_publishes_on_every_interface_without_starting_anything()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.Config = _ => [new ComposePublishedPort("nginx", "0.0.0.0", 3000, 80)];

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-004", error.Code);
        Assert.Contains("nginx on 0.0.0.0:3000", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Up", rig.Runtime.Calls, StringComparer.Ordinal);
        Assert.DoesNotContain("start", rig.Workspace.State!.Steps.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_stops_when_compose_config_fails()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.FailNext("PublishedPorts", Core.SindriErrors.OnyxComposeFailed.With("config failed"));

        Assert.Equal("SND-ONYX-011", (await rig.Up(Token)).Error.Code);
        Assert.DoesNotContain("Up", rig.Runtime.Calls, StringComparer.Ordinal);
    }
}
