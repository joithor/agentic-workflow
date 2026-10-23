using Aw.Context.Core.Linear;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Linear;

/// <summary>The canonical keys of the native Linear adapter's records (spec §5.1, D16): one key per record, shared with Onyx's issues.</summary>
public sealed class LinearRecordKeysTests
{
    private const string Id = "0E5A1B00-0000-4000-8000-000000000105";
    private const string Lower = "0e5a1b00-0000-4000-8000-000000000105";

    private static ExternalId Ext(string id) => ExternalId.TryFrom(id, out var ext) ? ext : ExternalId.From("not-a-uuid");

    [Fact, Trait("Lane", "unit")]
    public void Each_kind_has_its_own_prefix_over_the_lower_case_uuid()
    {
        Assert.Equal(
            ["linear:issue/" + Lower, "linear:doc/" + Lower, "linear:project/" + Lower, "linear:update/" + Lower],
            new[] { LinearRecordKeys.Issue(Ext(Id)), LinearRecordKeys.Document(Ext(Id)), LinearRecordKeys.Project(Ext(Id)), LinearRecordKeys.Update(Ext(Id)) }.Select(k => k!.Value.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_issue_has_the_key_onyxs_linear_connector_gives_it_so_the_two_adapters_merge()
    {
        var onyx = OnyxRecordKeys.Identify(ExternalId.From(Lower), "linear", "[SPL-105] Resize panes", "https://linear.app/x/issue/SPL-105", new HashSet<string>(StringComparer.Ordinal) { "SPL" })!;

        Assert.Equal(onyx.Canonical, LinearRecordKeys.Issue(Ext(Id)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SPL-105")]
    [InlineData("")]
    [InlineData("0e5a1b00-0000-4000-8000")]
    public void An_id_that_is_not_a_uuid_has_no_key(string id)
    {
        Assert.Null(LinearRecordKeys.Issue(Ext(id)));
        Assert.Null(LinearRecordKeys.Document(Ext(id)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_kind_follows_the_prefix()
    {
        Assert.Equal(
            [RecordKind.Issue, RecordKind.Doc, RecordKind.Project, RecordKind.Project],
            new[] { LinearRecordKeys.Issue(Ext(Id)), LinearRecordKeys.Document(Ext(Id)), LinearRecordKeys.Project(Ext(Id)), LinearRecordKeys.Update(Ext(Id)) }.Select(k => LinearRecordKeys.KindOf(k!.Value)));
    }
}
