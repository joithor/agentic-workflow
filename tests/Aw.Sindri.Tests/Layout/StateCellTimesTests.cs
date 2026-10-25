using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Layout;

/// <summary>The state band's times (F5 PR A): each cell carries when the latest visit to its state began and ended, from the visits the workflow keeps.</summary>
[Lane(TestLane.Unit)]
public sealed class StateCellTimesTests
{
    private static readonly DateTimeOffset _entered = StatusCases.Now - TimeSpan.FromHours(2);
    private static readonly DateTimeOffset _left = StatusCases.Now - TimeSpan.FromHours(1);

    private static InstanceView View(IReadOnlyDictionary<string, StateVisit>? visits) =>
        InstanceViews.Of(EngineFixture.At(StatusCases.Recipe.Recipe, "checks"), StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext(StateVisits: visits));

    [Fact]
    public void A_state_left_behind_carries_when_its_latest_visit_began_and_ended_and_the_current_one_carries_no_leave_time()
    {
        var visits = new Dictionary<string, StateVisit>(StringComparer.Ordinal)
        {
            ["produce"] = new(_entered, _left),
            ["checks"] = new(_left),
        };

        var cells = View(visits).States.ToDictionary(static c => c.State, StringComparer.Ordinal);

        Assert.Equal((_entered, _left), (cells["produce"].EnteredAt, cells["produce"].LeftAt));
        Assert.Equal((_left, null), (cells["checks"].EnteredAt, cells["checks"].LeftAt));
    }

    [Fact]
    public void A_state_the_instance_never_reached_carries_no_times()
    {
        var cells = View(new Dictionary<string, StateVisit>(StringComparer.Ordinal) { ["produce"] = new(_entered, _left) }).States;

        Assert.All(cells.Where(static c => c.State != "produce"), static c => Assert.Equal((null, null), (c.EnteredAt, c.LeftAt)));
    }

    [Fact]
    public void Without_visits_no_cell_carries_times_and_the_json_leaves_the_fields_out()
    {
        var view = View(visits: null);

        Assert.All(view.States, static c => Assert.Equal((null, null), (c.EnteredAt, c.LeftAt)));
        var json = Json(view.States[0]);
        Assert.DoesNotContain("enteredAt", json, StringComparison.Ordinal);
        Assert.DoesNotContain("leftAt", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_times_serialize_as_iso_8601_utc()
    {
        var cell = View(new Dictionary<string, StateVisit>(StringComparer.Ordinal) { ["produce"] = new(_entered, _left) }).States.Single(static c => c.State == "produce");

        var json = Json(cell);

        var stamp = _entered.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains($"\"enteredAt\":\"{stamp}", json, StringComparison.Ordinal);
        Assert.Contains("\"leftAt\":", json, StringComparison.Ordinal);
    }

    private static string Json<T>(T value) => System.Text.Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));
}
