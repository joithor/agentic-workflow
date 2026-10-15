using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>The wire vocabulary of the rev-8 views (API spec section 3.8): kebab-case enums, grantTokens, an unknown fact type as payload, nulls present.</summary>
public sealed class WorkflowViewJsonTests
{
    private static readonly JsonSerializerOptions _webOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);

    [Theory, Trait("Lane", "unit")]
    [InlineData(EngineOwnedKind.TakenOver, "\"taken-over\"")]
    [InlineData(EngineOwnedKind.Parked, "\"parked\"")]
    public void Engine_owned_kinds_are_kebab_case(EngineOwnedKind kind, string json) =>
        Assert.Equal(json, JsonSerializer.Serialize(kind, _options));

    [Fact, Trait("Lane", "unit")]
    public void Path_ordering_and_fact_types_are_kebab_case()
    {
        Assert.Equal("\"transition-list\"", JsonSerializer.Serialize(PathOrdering.TransitionList, _options));
        Assert.Equal("\"scope-map\"", JsonSerializer.Serialize(FactType.ScopeMap, _options));
        Assert.Equal("\"in-progress\"", JsonSerializer.Serialize(FactStatus.InProgress, _options));
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_kinds_are_written_as_the_status_layout_names_them()
    {
        Assert.Equal("\"grantTokens\"", JsonSerializer.Serialize(AnswerKind.GrantTokens, _options));
        Assert.Equal("\"cancel-only\"", JsonSerializer.Serialize(AnswerKind.CancelOnly, _options));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_fact_type_reads_as_payload() =>
        Assert.Equal(FactType.Payload, JsonSerializer.Deserialize<FactType>("\"hologram\"", _options));

    [Fact, Trait("Lane", "unit")]
    public void Absent_values_are_present_as_null_and_lists_as_empty_arrays()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(WorkflowViewFixtures.Done(), _options));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("itemId").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("engineOwned").ValueKind);
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("children").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("counts").GetProperty("context").ValueKind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_view_round_trips()
    {
        var view = WorkflowViewFixtures.ProjectBuild();

        var back = JsonSerializer.Deserialize<InstanceDetail>(JsonSerializer.Serialize(view, _options), _options);

        Assert.Equal(JsonSerializer.Serialize(view, _options), JsonSerializer.Serialize(back, _options));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_root_list_is_in_urgency_then_since_order()
    {
        var addresses = WorkflowViewFixtures.RootList().Instances.Select(r => r.Address);

        // The incident, then the triage rows by oldest since, then the work rows by oldest since (spelled out, so a change of the sort key shows).
        Assert.Equal(
            [
                "workflow/INC-14", "workflow/sentry-8790", "workflow/sentry-8797", "workflow/sentry-8801", "workflow/pagerduty-P-2211",
                "workflow/sentry-8809", "workflow/sentry-8812", "workflow/PRJ-exports", "workflow/PRJ-sync", "workflow/PRJ-tabs", "workflow/LIN-ENG",
                "workflow/LIN-APP",
            ],
            addresses,
            StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(FactType.Text), InlineData(FactType.File), InlineData(FactType.Item), InlineData(FactType.Link), InlineData(FactType.ScopeMap),
        InlineData(FactType.Packet), InlineData(FactType.Evidence), InlineData(FactType.Decision), InlineData(FactType.Payload)]
    public void Every_fact_type_round_trips(FactType type) =>
        Assert.Equal(type, JsonSerializer.Deserialize<FactType>(JsonSerializer.Serialize(type, _options), _options));

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"type\":{\"a\":1},\"n\":2}")]
    [InlineData("{\"type\":[1,2],\"n\":2}")]
    [InlineData("{\"type\":null,\"n\":2}")]
    public void A_fact_type_that_is_not_a_name_reads_as_payload_and_leaves_the_rest_readable(string json)
    {
        var typed = JsonSerializer.Deserialize<TypeHolder>(json, _webOptions);

        Assert.Equal(FactType.Payload, typed!.Type);
        Assert.Equal(2, typed.N);
    }

    private sealed record TypeHolder(FactType Type, int N);

    [Theory, Trait("Lane", "unit")]
    [InlineData("mitigate", null, false, false, "RUNNING", ChipTone.Ok)]
    [InlineData("classify", null, true, false, "DECISION", ChipTone.Attn)]
    [InlineData("done", null, false, true, "DONE", ChipTone.Neutral)]
    [InlineData("queued", null, false, false, "QUEUED", ChipTone.Neutral)]
    [InlineData("route", EngineOwnedKind.Escalated, true, false, "ESCALATED", ChipTone.Attn)]
    [InlineData("build", EngineOwnedKind.Parked, false, false, "PARKED", ChipTone.Attn)]
    [InlineData("design", EngineOwnedKind.TakenOver, true, false, "WITH YOU", ChipTone.Neutral)]
    public void The_chip_follows_the_one_mapping(string state, EngineOwnedKind? owned, bool deskOpen, bool terminal, string word, ChipTone tone)
    {
        var badge = owned is { } kind ? new EngineOwnedBadge(kind, "why") : null;

        Assert.Equal(new StateChip(word, tone), StateChip.For(state, badge, deskOpen, terminal));
    }
}
