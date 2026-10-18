using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Graph;

public sealed class NodeKindsTests
{
    private static readonly string[] _surfaceKinds =
        ["api", "command", "dialog", "flag", "job", "keybinding", "menu", "notification", "pane", "permission", "report", "route", "screen", "setting"];

    [Theory, Trait("Lane", "unit")]
    [InlineData("surface:notification/digest-email")]
    [InlineData("surface:permission/share-link")]
    [InlineData("surface:report/audit")]
    [InlineData("surface:flag/link-expiry")]
    public void The_four_C2_kinds_are_surface_kinds(string key)
    {
        Assert.True(NodeKey.TryFrom(key, out var parsed));

        Assert.True(NodeKinds.HasValidSurfaceKind(parsed));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_kind_set_is_closed()
    {
        Assert.Equal(_surfaceKinds, NodeKinds.SurfaceKinds.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.True(NodeKey.TryFrom("surface:widget/x", out var unknown));
        Assert.False(NodeKinds.HasValidSurfaceKind(unknown));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_map_names_the_four_kinds_and_the_refusal_lists_them()
    {
        const string Header = "schemaVersion: 1\nkind: authored\nrepo: r\n";
        var ok = MapDocumentValidator.Check(Maps.Parse(Header + "nodes:\n  - {key: \"surface:flag/link-expiry\", label: Expiry}\n  - {key: \"surface:notification/digest-email\", label: Digest}\n"), KeyMigrations.Shipped);
        var refused = MapDocumentValidator.Check(Maps.Parse(Header + "nodes:\n  - {key: \"surface:widget/x\", label: X}\n"), KeyMigrations.Shipped);

        Assert.True(ok.IsSuccess);
        Assert.True(refused.IsError);
        Assert.All(["notification", "permission", "report", "flag"], kind => Assert.Contains(kind, refused.Error.Message, StringComparison.Ordinal));
    }
}
