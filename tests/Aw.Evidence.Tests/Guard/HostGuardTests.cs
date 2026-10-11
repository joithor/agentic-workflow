using Aw.Evidence.Core;
using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Tests.Guard;

public sealed class HostGuardTests
{
    [Fact, Trait("Lane", "unit")]
    public void No_host_flag_is_the_local_stack_on_port_3000() =>
        Assert.Equal(new ResolvedHost("http://localhost:3000", HostKind.Local), HostGuard.Resolve(host: null, allowPreviewHost: false).Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://localhost:4000", "http://localhost:4000")]
    [InlineData("http://localhost:4000/", "http://localhost:4000")]
    [InlineData("http://127.0.0.1:5173", "http://127.0.0.1:5173")]
    [InlineData("http://LOCALHOST:8080", "http://localhost:8080")]
    public void A_loopback_origin_on_another_port_is_local_without_any_allow_flag(string host, string origin) =>
        Assert.Equal(new ResolvedHost(origin, HostKind.Local), HostGuard.Resolve(host, allowPreviewHost: false).Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData("https://pr-123.vitalize.build", "https://pr-123.vitalize.build")]
    [InlineData("https://pr-7.vitalize.build/", "https://pr-7.vitalize.build")]
    public void A_pr_preview_host_with_allow_preview_host_is_a_preview(string host, string origin) =>
        Assert.Equal(new ResolvedHost(origin, HostKind.Preview), HostGuard.Resolve(host, allowPreviewHost: true).Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData("https://pr-123.vitalize.build", false, "needs --allow-preview-host")]
    [InlineData("https://dev.vitalize.build", true, "not a pr-<n>.vitalize.build preview host")]
    [InlineData("https://app.vitalize.build", true, "dev and prod hosts are refused")]
    [InlineData("https://pr-12.vitalize.build.evil.com", true, "not a pr-<n>")]
    [InlineData("http://pr-123.vitalize.build", true, "must be https")]
    [InlineData("https://pr-123.vitalize.build:8443", true, "must not carry a port")]
    [InlineData("https://user:pw@pr-123.vitalize.build", true, "credentials")]
    [InlineData("https://pr-123.vitalize.build/admin", true, "bare origin")]
    [InlineData("https://pr-123.vitalize.build/?x=1", true, "bare origin")]
    [InlineData("https://pr-123.vitalize.build/#top", true, "bare origin")]
    [InlineData("http://localhost", false, "http://localhost:<port>")]
    [InlineData("https://localhost:3000", false, "http://localhost:<port>")]
    [InlineData("http://localhost:3000/app", false, "bare origin")]
    [InlineData("http://user@localhost:3000", false, "credentials")]
    [InlineData("http://192.168.1.10:3000", false, "must be https")]
    [InlineData("not a url", true, "is not a URL")]
    public void Any_other_host_is_refused_with_the_reason(string host, bool allowPreview, string reason)
    {
        var result = HostGuard.Resolve(host, allowPreview);

        Assert.Equal(EvidenceErrors.HostRefused.Code, result.Error.Code);
        Assert.Contains(reason, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://localhost:3000/next", true, false)]
    [InlineData("https://evil.example/x", true, true)]
    [InlineData("https://evil.example/x", false, false)]
    [InlineData("about:blank", true, false)]
    [InlineData("http://localhost:4000/", true, true)]
    [InlineData("::not a url::", true, true)]
    public void Navigation_off_the_vetted_origin_is_aborted(string url, bool isNavigation, bool aborted) =>
        Assert.Equal(aborted, HostGuard.IsOffOriginNavigation("http://localhost:3000", url, isNavigation));

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://localhost:3000", true)]
    [InlineData("http://127.0.0.1:4000", true)]
    [InlineData("https://pr-1.vitalize.build", false)]
    [InlineData("nonsense", false)]
    public void Only_this_machine_counts_as_local(string origin, bool local) =>
        Assert.Equal(local, HostGuard.IsLocal(origin));
}
