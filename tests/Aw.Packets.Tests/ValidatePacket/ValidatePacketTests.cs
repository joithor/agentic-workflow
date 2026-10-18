using Aw.Packets.App.Features.ValidatePacket;
using Aw.Packets.Core;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.ValidatePacket;

public sealed class ValidatePacketTests
{
    private static async Task<ValidatePacketResponse> Validate(PacketBuilder packet, InMemoryRepoSnapshot? repo = null)
    {
        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", PacketFiles.Of(packet.Build())),
            repo ?? new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)), new InMemorySizesSource());
        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return result.Value;
    }

    private static IReadOnlyList<CheckResult> Failures(ValidatePacketResponse report, string rule) =>
        [.. report.Checks.Where(c => c.Rule == rule && c.State == CheckState.Fail)];

    [Fact, Trait("Lane", "unit")]
    public async Task ValidatePacket_reports_a_cycle_in_the_task_graph()
    {
        var report = await Validate(A.Packet()
            .With(A.Task("t1").After("t3").Writing("src/a.cs"))
            .With(A.Task("t2").After("t1").Writing("src/a.cs"))
            .With(A.Task("t3").After("t2").Writing("src/a.cs")));

        Assert.False(report.Passed);
        var cycle = Assert.Single(Failures(report, "PKT-GRAPH-002"));
        Assert.Contains("t1 -> t3 -> t2 -> t1", cycle.Message, StringComparison.Ordinal);
        Assert.NotNull(cycle.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sound_packet_passes_every_implemented_rule_and_lists_the_undefined_ones_as_unchecked()
    {
        var report = await Validate(A.Packet());

        Assert.True(report.Passed);
        Assert.Equal(PacketId.From("demo-packet"), report.Packet);
        Assert.Equal(
            ["PKT-ID-001", "PKT-WS-001", "PKT-GRAPH-001", "PKT-GRAPH-002", "PKT-BASE-001", "PKT-SCOPE-001", "PKT-SCOPE-002",
             "PKT-ACC-001", "PKT-EVID-001", "PKT-DEC-001", "PKT-HOLD-001", "PKT-HOLD-002"],
            report.Checks.Where(static c => c.State == CheckState.Pass).Select(static c => c.Rule),
            StringComparer.Ordinal);
        Assert.Equal(
            ["PKT-SIZE-001", "PKT-FLOW-001"],
            report.Checks.Where(static c => c.State == CheckState.Unchecked).Select(static c => c.Rule),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dependency_on_an_unknown_task_fails_PKT_GRAPH_001()
    {
        var report = await Validate(A.Packet().With(A.Task("t1").After("ghost")));

        var failure = Assert.Single(Failures(report, "PKT-GRAPH-001"));
        Assert.Equal("task t1", failure.Subject);
        Assert.Contains("ghost", failure.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_task_in_an_undeclared_workstream_fails_PKT_WS_001()
    {
        var report = await Validate(A.Packet().With(A.Task("t1").In("ws-missing")));

        Assert.Equal("task t1", Assert.Single(Failures(report, "PKT-WS-001")).Subject);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Duplicate_ids_fail_PKT_ID_001()
    {
        var report = await Validate(A.Packet()
            .With(A.Task("t1").Writing("src/a.cs").Example("e1", holdout: true, "test"))
            .With(A.Task("t1").Writing("src/b.cs").Example("e1", holdout: false, "test")));

        Assert.Equal(["example e1", "task t1"], Failures(report, "PKT-ID-001").Select(static f => f.Subject), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "Not A Kind" }, false)]
    [InlineData(new[] { "test", "cli-transcript" }, true)]
    public async Task Every_acceptance_example_maps_to_an_evidence_kind(string[] evidence, bool passes)
    {
        var report = await Validate(A.Packet().With(A.Task("t1").Example("e1", holdout: true, evidence)));

        Assert.Equal(passes, Failures(report, "PKT-EVID-001").Count == 0);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("answered", true)]
    [InlineData("deferred", true)]
    [InlineData("open", false)]
    [InlineData("deferred-without-owner", false)]
    [InlineData("deferred-without-default", false)]
    public async Task Every_question_is_answered_or_deferred_with_an_owner_and_a_default(string state, bool passes)
    {
        var question = new Question
        {
            Id = QuestionId.From("q9"),
            Text = "Which store?",
            Answer = state == "answered" ? "SQLite" : null,
            Deferred = state switch
            {
                "deferred" => new Deferral("owner", "SQLite"),
                "deferred-without-owner" => new Deferral(" ", "SQLite"),
                "deferred-without-default" => new Deferral("owner", ""),
                _ => null,
            },
        };

        var report = await Validate(A.Packet().With(question));

        Assert.Equal(passes, Failures(report, "PKT-DEC-001").Count == 0);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_packet_without_a_held_out_example_fails_PKT_HOLD_001()
    {
        var report = await Validate(A.Packet().With(A.Task("t1").Example("e1", holdout: false, "test")));

        _ = Assert.Single(Failures(report, "PKT-HOLD-001"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_base_sha_missing_from_the_repo_fails_PKT_BASE_001_and_leaves_the_scope_rules_unchecked()
    {
        var report = await Validate(A.Packet(), new InMemoryRepoSnapshot());

        _ = Assert.Single(Failures(report, "PKT-BASE-001"));
        Assert.All(
            report.Checks.Where(static c => c.Rule.StartsWith("PKT-SCOPE-", StringComparison.Ordinal)),
            static c => Assert.Equal(CheckState.Unchecked, c.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_short_or_malformed_base_sha_fails_PKT_BASE_001_without_asking_the_repo()
    {
        var report = await Validate(A.Packet().Base("abc123"), new InMemoryRepoSnapshot().Unreadable("pkt"));

        Assert.Contains("full", Assert.Single(Failures(report, "PKT-BASE-001")).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_repo_is_AW_PACKETS_006_not_a_check_failure()
    {
        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", PacketFiles.Of(A.Packet().Build())),
            new InMemoryRepoSnapshot().Unreadable("pkt")), new InMemorySizesSource());

        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);

        Assert.Equal("AW-PACKETS-006", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_repo_directory_defaults_to_the_packet_directory_and_can_be_given()
    {
        var repo = new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles).Unreadable("pkt");
        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", PacketFiles.Of(A.Packet().Build())), repo), new InMemorySizesSource());

        var defaulted = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);
        var given = await service.Handle(new ValidatePacketRequest("pkt", "repo"), CancellationToken.None);

        Assert.True(defaulted.IsError);
        Assert.True(given.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_task_in_a_strongly_connected_group_is_named_once()
    {
        var report = await Validate(A.Packet()
            .With(A.Task("t1").After("t2", "t3").Writing("src/a.cs").Example("h", holdout: true, "test"))
            .With(A.Task("t2").After("t4").Writing("src/a.cs"))
            .With(A.Task("t3").After("t4").Writing("src/a.cs"))
            .With(A.Task("t4").After("t1").Writing("src/a.cs")));

        var cycle = Assert.Single(Failures(report, "PKT-GRAPH-002"));
        Assert.Contains("t1, t2, t3, t4", cycle.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, "it works", true)]
    [InlineData(false, null, false)]
    [InlineData(false, " ", false)]
    public async Task Every_visible_example_has_text(bool holdout, string? text, bool passes)
    {
        var report = await Validate(A.Packet().With(A.Task("t1").ExampleWithText("e1", holdout, text).Example("h", holdout: true, "test")));

        Assert.Equal(passes, Failures(report, "PKT-ACC-001").Count == 0);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, true)]
    [InlineData("the secret expected output", false)]
    public async Task A_held_out_example_keeps_no_text_in_the_packet(string? text, bool passes)
    {
        var report = await Validate(A.Packet().With(A.Task("t1").ExampleWithText("h1", holdout: true, text)));

        Assert.Equal(passes, Failures(report, "PKT-HOLD-002").Count == 0);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_task_with_acceptance_examples_has_a_held_out_one()
    {
        var report = await Validate(A.Packet()
            .With(A.Task("t1").Writing("src/a.cs"))
            .With(A.Task("t2").After("t1").Writing("src/b.cs").Example("e2", holdout: false, "test")));

        Assert.Equal("task t2", Assert.Single(Failures(report, "PKT-HOLD-001")).Subject);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_task_with_no_acceptance_examples_fails()
    {
        var report = await Validate(A.Packet()
            .With(A.Task("t1").Writing("src/a.cs"))
            .With(A.Task("t2").After("t1").Writing("src/b.cs").NoExamples()));

        Assert.Equal("task t2", Assert.Single(Failures(report, "PKT-HOLD-001")).Subject);
        Assert.Equal("task t2", Assert.Single(Failures(report, "PKT-ACC-001")).Subject);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_task_whose_only_example_is_held_out_fails_PKT_ACC_001()
    {
        var report = await Validate(A.Packet().With(A.Task("t1").ExampleWithText("h1", holdout: true, text: null)));

        Assert.Equal("task t1", Assert.Single(Failures(report, "PKT-ACC-001")).Subject);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_evidence_kind_with_a_trailing_newline_fails() =>
        Assert.Single(Failures(await Validate(A.Packet().With(A.Task("t1").Example("e1", holdout: false, "test\n").Example("h", holdout: true, "test"))), "PKT-EVID-001"));
}
