using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Tests.Scripts;

public sealed class ExpectedStateKindOrderTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_expected_state_reads_with_kind_after_its_other_keys()
    {
        var script = ScriptReader.Read(TestJson.Utf8(
            """{"route":"/","role":"manager","viewports":["desktop"],"steps":[{"expectedState":{"testId":"search","value":"St","kind":"input-value"},"action":"fill","target":"search","value":"St"}]}"""),
            TestJson.Options);

        Assert.True(script.IsSuccess, script.IsError ? script.Error.Message : null);
        Assert.Equal(new InputValue(TestId.From("search"), "St"), script.Value.Steps[0].ExpectedState);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_expected_state_still_writes_kind_first()
    {
        var step = new ScriptStep(StepAction.Goto, "/", new UrlPath("/"));

        Assert.Contains("\"expectedState\":{\"kind\":\"url-path\",\"path\":\"/\"}", TestJson.Write(step), StringComparison.Ordinal);
    }
}
