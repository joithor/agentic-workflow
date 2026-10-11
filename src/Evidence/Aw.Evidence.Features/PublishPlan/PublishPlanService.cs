using System.Text.Json;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Core.Runs;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.PublishPlan;

/// <summary>Reads summary.json and plans its publication.</summary>
internal sealed class PublishPlanService(IRunFiles files, JsonSerializerOptions json) : IService<PublishPlanRequest, PublishPlanResponse>
{
    public async ValueTask<Result<PublishPlanResponse>> Handle(PublishPlanRequest request, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(RunPath.Combine(request.RunDirectory, "summary.json"), cancellationToken).ConfigureAwait(false);
        return bytes
            .Bind(b => RunFileReader.Summary(b, json))
            .Map(s => new PublishPlanResponse(RunPublishing.Plan(s, request.Provenance, !string.IsNullOrWhiteSpace(request.LinearIssue))));
    }
}
