using System.Text.Json;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>The words the sindri_* tools write in two places, held to the one the API writes (API P8).</summary>
public sealed class SindriToolSchemaTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_kind_enum_in_the_tool_schema_is_the_request_kind_names_of_the_json_context()
    {
        var advertised = SindriTools.RequestTool.InputSchema.GetProperty("properties").GetProperty("kind").GetProperty("enum")
            .EnumerateArray().Select(k => k.GetString()).ToList();

        var bound = Enum.GetValues<RequestKind>().Select(k => JsonSerializer.SerializeToElement(k, SindriJsonContext.Default.RequestKind).GetString()).ToList();

        Assert.Equal(bound.Order(StringComparer.Ordinal), advertised.Order(StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_job_state_is_summarised_by_its_json_name()
    {
        foreach (var state in Enum.GetValues<JobState>())
        {
            Assert.Equal(JsonSerializer.Serialize(state).Trim('"'), McpSummaries.JobStateWord(state));
        }
    }
}
