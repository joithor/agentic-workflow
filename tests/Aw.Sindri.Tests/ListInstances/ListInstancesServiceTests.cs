using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Tests.GetInstance;
using RecipeUrgency = Aw.Sindri.Core.Workflow.Recipes.RecipeUrgency;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>
/// 'workflow list' (engine spec §6.3, API spec §3.8) over a recording port: how the request becomes a typed filter or a raw
/// query, when ended instances are listed, the order of the rows, and the subject match that the search attribute can't do.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ListInstancesServiceTests
{
    private static InstanceView View(string name, string address, RecipeUrgency urgency = RecipeUrgency.Work, string? subjectKey = null, string? label = null) =>
        StatusCases.View(name) with
        {
            Address = InstanceAddress.From(address),
            Urgency = urgency,
            Subject = subjectKey is null ? null : new SubjectRef("file", subjectKey, label ?? subjectKey, Href: null),
        };

    private static async Task<(Result<ListInstancesResponse> Result, RecordingInstances Port)> Run(ListInstancesRequest request, params InstanceView[] views)
    {
        var port = new RecordingInstances([.. views.Select(static v => new InstanceRow(v.Address.Value, v.State, NeedsYou: false))]);
        var service = new ListInstancesService(new NoDirectory(), port, new FixedReader(views));
        return (await service.Handle(request, CancellationToken.None), port);
    }

    [Fact]
    public async Task An_unknown_urgency_is_invalid_input_and_the_port_is_not_asked()
    {
        var (result, port) = await Run(new ListInstancesRequest(Urgency: "urgent"));

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(port.Calls);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("TRUE")]
    [InlineData("")]
    public async Task A_root_that_is_not_true_or_false_is_invalid_input(string root)
    {
        var (result, port) = await Run(new ListInstancesRequest(Root: root));

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(port.Calls);
    }

    [Fact]
    public async Task A_needs_me_that_is_not_true_or_false_is_invalid_input()
    {
        var (result, _) = await Run(new ListInstancesRequest(NeedsMe: "maybe"));

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact]
    public async Task Every_typed_filter_but_the_subject_reaches_the_port_as_the_typed_filter()
    {
        var (result, port) = await Run(new ListInstancesRequest(
            NeedsMe: "true", Recipe: "workflow.step", Urgency: "incident", Subject: "fact:spec", Root: "false"));

        Assert.True(result.IsSuccess);
        var call = Assert.Single(port.Calls);
        Assert.Equal(new InstanceFilter("workflow.step", State: null, NeedsYou: true, RecipeUrgency.Incident, Item: null, Subject: null, Root: false), call.Filter);
    }

    [Fact]
    public async Task The_item_and_the_state_reach_the_port_and_list_ended_instances_too()
    {
        var (_, byItem) = await Run(new ListInstancesRequest(Item: ItemKey.From("ITM-1")));
        var (_, byState) = await Run(new ListInstancesRequest(State: "done"));
        var (_, neither) = await Run(new ListInstancesRequest(Recipe: "workflow.step"));

        Assert.Equal([("ITM-1", true), ("done", true)], [(byItem.Calls[0].Filter!.Item!, byItem.Calls[0].Ended), (byState.Calls[0].Filter!.State!, byState.Calls[0].Ended)]);
        Assert.False(neither.Calls[0].Ended);
    }

    [Fact]
    public async Task A_needs_me_of_false_is_no_filter_and_a_state_still_lists_ended_instances()
    {
        var (_, port) = await Run(new ListInstancesRequest(NeedsMe: "false", State: "parked"));

        var call = Assert.Single(port.Calls);
        Assert.Equal((new InstanceFilter(State: "parked"), true), (call.Filter, call.Ended));
    }

    [Fact]
    public async Task A_raw_query_alone_is_parenthesised_and_with_typed_filters_follows_them_joined_by_and()
    {
        var (_, alone) = await Run(new ListInstancesRequest(Query: "NeedsYou = true OR State = 'done'"));
        var (_, joined) = await Run(new ListInstancesRequest(Query: "NeedsYou = true OR State = 'done'", Recipe: "workflow.step", Root: "true"));

        Assert.Equal("(NeedsYou = true OR State = 'done')", alone.Calls[0].Query);
        Assert.Equal("Recipe = 'workflow.step' AND (Root = true OR Root IS NULL) AND (NeedsYou = true OR State = 'done')", joined.Calls[0].Query);
    }

    [Fact]
    public async Task Root_false_asks_for_the_children_only()
    {
        var (_, port) = await Run(new ListInstancesRequest(Root: "false"));

        Assert.False(port.Calls[0].Filter!.Root);
        Assert.Contains("Root = false", InstanceFilters.ToQuery(port.Calls[0].Filter!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_typed_list_is_in_the_section_3_8_order_whatever_order_the_port_returns()
    {
        var (result, _) = await Run(
            new ListInstancesRequest(Recipe: "workflow.step"),
            View("done", "T-1/step/1", RecipeUrgency.Incident),
            View("parked-rounds", "T-2/step/1", RecipeUrgency.Incident),
            View("desk-task", "T-3/step/1"),
            View("desk-task", "T-4/step/1", RecipeUrgency.Triage));

        Assert.Equal(["T-4/step/1", "T-3/step/1", "T-2/step/1", "T-1/step/1"], result.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_raw_query_list_is_in_the_same_order()
    {
        var (result, _) = await Run(
            new ListInstancesRequest(Query: "State = 'produce'"),
            View("done", "T-1/step/1", RecipeUrgency.Incident),
            View("desk-task", "T-3/step/1"),
            View("desk-task", "T-4/step/1", RecipeUrgency.Triage));

        Assert.Equal(["T-4/step/1", "T-3/step/1", "T-1/step/1"], result.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Needs_me_keeps_its_sections_with_decisions_first_by_address()
    {
        var (result, _) = await Run(
            new ListInstancesRequest(NeedsMe: "true"),
            View("desk-task", "T-1/step/1", RecipeUrgency.Incident),
            View("parked-rounds", "T-3/step/1"),
            View("escalated-gate", "T-2/step/1"));

        Assert.Equal(["T-2/step/1", "T-3/step/1", "T-1/step/1"], result.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_rows_are_the_summaries_of_the_listed_views_in_the_same_order()
    {
        var (result, _) = await Run(
            new ListInstancesRequest(Recipe: "workflow.step"),
            View("desk-task", "T-3/step/1"),
            View("desk-task", "T-4/step/1", RecipeUrgency.Triage));

        Assert.Equal(
            result.Value.Instances.Select(static v => InstanceSummaries.Of(v)).Select(static r => (r.Address, r.State, r.Now, r.Recipe, r.Since)),
            result.Value.Rows.Select(static r => (r.Address, r.State, r.Now, r.Recipe, r.Since)));
        Assert.Equal(["T-4/step/1", "T-3/step/1"], result.Value.Rows.Select(static r => r.Address), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_needs_me_list_has_the_rows_of_its_sections_in_their_order()
    {
        var (result, _) = await Run(
            new ListInstancesRequest(NeedsMe: "true"),
            View("desk-task", "T-1/step/1"),
            View("parked-rounds", "T-3/step/1"));

        Assert.Equal(["T-3/step/1", "T-1/step/1"], result.Value.Rows.Select(static r => r.Address), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_subject_matches_the_subject_key_or_a_part_of_its_label_and_is_not_sent_to_the_port()
    {
        var views = new[]
        {
            View("desk-task", "T-1/step/1", subjectKey: "fact:spec", label: "Offline sync spec"),
            View("desk-task", "T-2/step/1", subjectKey: "fact:ticket", label: "APP-12"),
            View("desk-task", "T-3/step/1"),
        };

        var (byKey, port) = await Run(new ListInstancesRequest(Subject: "fact:ticket"), views);
        var (byLabel, _) = await Run(new ListInstancesRequest(Subject: "sync"), views);
        var (none, _) = await Run(new ListInstancesRequest(Subject: "nothing like it"), views);

        Assert.Equal(["T-2/step/1"], byKey.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
        Assert.Equal(["T-1/step/1"], byLabel.Value.Instances.Select(static v => v.Address.Value), StringComparer.Ordinal);
        Assert.Equal(("no instances match", 0), (none.Value.Empty, none.Value.Instances.Count));
        Assert.Null(port.Calls[0].Filter!.Subject);
    }
}
