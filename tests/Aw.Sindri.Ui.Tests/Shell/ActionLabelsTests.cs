using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>K8 (Linux proof): the app never shows a raw catalog id such as "ApplyCommand:command" or "IssueNonce" as a label.</summary>
[Lane(TestLane.Unit)]
public sealed class ActionLabelsTests
{
    private static readonly CommandList _catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;

    [Fact]
    public void Every_action_id_in_the_catalog_has_a_label_that_is_not_its_id()
    {
        Assert.All(_catalog.Rows, row =>
        {
            var label = Labels.Action(row.Action);
            Assert.NotEqual(row.Action, label, StringComparer.Ordinal);
            Assert.DoesNotContain(':', label);
        });
    }

    [Fact]
    public void Every_label_is_its_own()
    {
        var labels = _catalog.Rows.Select(r => Labels.Action(r.Action)).ToList();

        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }

    // An id this build has no label for shows a humanised form of itself, never the raw "X:y".
    [Theory]
    [InlineData("SomeNewThing", "Some new thing")]
    [InlineData("ApplyCommand:pause-all", "Apply command pause all")]
    [InlineData("RunDoctor2", "Run doctor2")]
    public void An_id_without_a_label_is_humanised(string action, string label) => Assert.Equal(label, Labels.Action(action), StringComparer.Ordinal);
}
