using Aw.BugFix.Core.Ids;
using Aw.BugFix.Host;
using Aw.Kernel.Json;

namespace Aw.BugFix.Tests.Rules;

public sealed class IdsTests
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(BugFixJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void Typed_ids_round_trip_as_json_strings()
    {
        var verdict = new Core.Model.JudgeVerdict(Core.Model.JudgeDecision.Resolved, JudgeDecisionId.From("d-42"), "ok", "sha");
        var resume = new Features.RecordRun.RecordRunResponse(CandidateId.Numbered(12), Passed: true, "judge c12");

        var verdictJson = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(verdict, _json));
        var resumeJson = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(resume, _json));

        Assert.Contains("\"decisionId\":\"d-42\"", verdictJson, StringComparison.Ordinal);
        Assert.Contains("\"candidate\":\"c12\"", resumeJson, StringComparison.Ordinal);
        Assert.Equal(verdict, AwJson.Deserialize<Core.Model.JudgeVerdict>(System.Text.Encoding.UTF8.GetBytes(verdictJson), _json).Value);
        Assert.Equal(resume, AwJson.Deserialize<Features.RecordRun.RecordRunResponse>(System.Text.Encoding.UTF8.GetBytes(resumeJson), _json).Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("c0")]
    [InlineData("C1")]
    [InlineData("c1 ")]
    [InlineData("")]
    public void A_malformed_candidate_id_is_refused(string text)
    {
        Assert.False(CandidateId.TryFrom(text, out _));
        Assert.True(AwJson.Deserialize<Features.RecordRun.RecordRunResponse>(
            System.Text.Encoding.UTF8.GetBytes($$"""{"candidate":"{{text}}","passed":true,"next":"x"}"""), _json).IsError);
    }
}
