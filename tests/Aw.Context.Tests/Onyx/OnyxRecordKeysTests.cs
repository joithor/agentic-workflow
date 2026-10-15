using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Onyx;

/// <summary>
/// Canonical keys and identifier aliases for Onyx hits (spec §5.1, D16): a Linear document keys on its issue UUID and
/// takes the identifier as an alias only when the connector-built title prefix and the issue link agree.
/// </summary>
public sealed class OnyxRecordKeysTests
{
    private const string Uuid = "0e5a1b00-0000-4000-8000-000000000105";
    private const string Link = "https://linear.app/fixture-labs/issue/SPL-105";
    private const string Title = "[SPL-105] Resize panes with the keyboard";
    private static readonly HashSet<string> _teams = new(StringComparer.Ordinal) { "SPL", "APP" };

    [Fact, Trait("Lane", "unit")]
    public void A_linear_document_keys_on_its_issue_uuid_with_the_identifier_as_alias()
    {
        var identity = Identify(Uuid, "linear", Title, Link);

        Assert.Equal("linear:issue/" + Uuid, identity!.Canonical.Value);
        Assert.Equal(RecordKind.Issue, identity.Kind);
        Assert.Equal("SPL-105", identity.Alias);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_upper_case_uuid_keys_in_lower_case()
    {
        var identity = Identify(Uuid.ToUpperInvariant(), "linear", Title, Link + "/resize-panes");

        Assert.Equal("linear:issue/" + Uuid, identity!.Canonical.Value);
        Assert.Equal("SPL-105", identity.Alias);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_linear_document_whose_id_is_not_a_uuid_keys_as_an_onyx_record_without_alias()
    {
        var identity = Identify("SPL-105", "linear", Title, Link);

        Assert.Equal("onyx:linear/SPL-105", identity!.Canonical.Value);
        Assert.Equal(RecordKind.Issue, identity.Kind);
        Assert.Null(identity.Alias);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("[SPL-105] Resize panes", "https://linear.app/fixture-labs/issue/SPL-104")]
    [InlineData("[SPL-105] Resize panes", "http://linear.app/fixture-labs/issue/SPL-105")]
    [InlineData("[SPL-105] Resize panes", "https://linear.app.evil.example/fixture-labs/issue/SPL-105")]
    [InlineData("[SPL-105] Resize panes", "https://evil.example/fixture-labs/issue/SPL-105")]
    [InlineData("[SPL-105] Resize panes", "https://linear.app/fixture-labs/project/SPL-105")]
    [InlineData("[SPL-105] Resize panes", "https://linear.app/issue/SPL-105")]
    [InlineData("[SPL-105] Resize panes", null)]
    [InlineData("Resize panes SPL-105", "https://linear.app/fixture-labs/issue/SPL-105")]
    [InlineData("[spl-105] Resize panes", "https://linear.app/fixture-labs/issue/spl-105")]
    [InlineData("[SPL105] Resize panes", "https://linear.app/fixture-labs/issue/SPL105")]
    [InlineData("[ENG-105] Resize panes", "https://linear.app/fixture-labs/issue/ENG-105")]
    [InlineData("[SPL-105]Resize panes", "https://linear.app/fixture-labs/issue/SPL-105")]
    public void A_prefix_and_link_that_disagree_or_break_a_rule_give_no_alias(string title, string? link)
    {
        var identity = Identify(Uuid, "linear", title, link);

        Assert.Equal("linear:issue/" + Uuid, identity!.Canonical.Value);
        Assert.Null(identity.Alias);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_slack_thread_keys_on_its_channel_and_thread_ts()
    {
        var identity = Identify("C0FIXTURE01__1784972400.000100", "slack", "#split-panes: resize step", "https://fixture-labs.slack.com/archives/C0FIXTURE01/p1784972400000100");

        Assert.Equal("slack:C0FIXTURE01/1784972400.000100", identity!.Canonical.Value);
        Assert.Equal(RecordKind.Message, identity.Kind);
        Assert.Null(identity.Alias);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("slack", "not-a-thread", "onyx:slack/not-a-thread", RecordKind.Message)]
    [InlineData("confluence", "https://wiki.example/x", "onyx:confluence/https://wiki.example/x", RecordKind.Doc)]
    [InlineData("web", "[SPL-105] page", "onyx:web/[SPL-105] page", RecordKind.Doc)]
    public void Any_other_record_keys_as_onyx_source_type_and_document_id(string source, string id, string key, RecordKind kind)
    {
        var identity = Identify(id, source, Title, Link);

        Assert.Equal(key, identity!.Canonical.Value);
        Assert.Equal(kind, identity.Kind);
        Assert.Null(identity.Alias);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_document_id_too_long_for_a_key_has_no_identity() =>
        Assert.Null(Identify(new string('a', 1024), "web", "t", link: null));

    [Theory, Trait("Lane", "unit")]
    [InlineData("linear:issue/" + Uuid, RecordKind.Issue)]
    [InlineData("onyx:linear/SPL-105", RecordKind.Issue)]
    [InlineData("slack:C0FIXTURE01/1784972400.000100", RecordKind.Message)]
    [InlineData("onyx:slack/x", RecordKind.Message)]
    [InlineData("onyx:confluence/x", RecordKind.Doc)]
    public void The_kind_follows_from_the_canonical_key(string key, RecordKind kind) =>
        Assert.Equal(kind, OnyxRecordKeys.KindOf(RecordKey.From(key)));

    private static OnyxRecordIdentity? Identify(string id, string source, string title, string? link) =>
        OnyxRecordKeys.Identify(ExternalId.From(id), source, title, link, _teams);
}
