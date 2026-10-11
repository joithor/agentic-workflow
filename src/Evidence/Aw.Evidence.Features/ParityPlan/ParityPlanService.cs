using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Core.Runs;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ParityPlan;

/// <summary>Reads parity.json and plans one Linear attachment per frame; unknown provenance keeps the evidence local.</summary>
internal sealed class ParityPlanService(IRunFiles files, JsonSerializerOptions json) : IService<ParityPlanRequest, ParityPlanResponse>
{
    public async ValueTask<Result<ParityPlanResponse>> Handle(ParityPlanRequest request, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(RunPath.Combine(request.RunDirectory, ParitySummary.FileName), cancellationToken).ConfigureAwait(false);
        var summary = bytes.Bind(b => RunFileReader.Parity(b, json));
        if (summary.IsError)
        {
            return summary.Error;
        }

        if (!PublishPolicy.CanUploadToLinear(summary.Value.Provenance))
        {
            return EvidenceErrors.PublishRefused.With("provenance unknown: evidence stays local, no Linear upload");
        }

        var sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var file in summary.Value.Frames.Select(static f => f.Files.SideBySide))
        {
            var size = await files.SizeAsync(file, cancellationToken).ConfigureAwait(false);
            if (size.IsError)
            {
                return size.Error;
            }

            sizes[file] = size.Value;
        }

        var plan = ParityAttachments.Plan(summary.Value, file => sizes[file]);
        var unreviewed = ParityAttachments.Unreviewed(plan, [.. request.Reviewed]);
        return new ParityPlanResponse(summary.Value.Provenance, AskFirst: true, unreviewed.Count > 0, unreviewed, plan);
    }
}
