using System.Text.Json;
using Aw.Evidence.Features.PublishPlan;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'publish-plan' command: prints the publish plan for a run (a non-local host's provenance is always unknown).</summary>
internal static class PublishPlanCommand
{
    internal const string Name = "publish-plan";
    internal const string Shape = "publish-plan <run-dir> --provenance seeded|unknown [--linear-issue id]";

    private static readonly CliSpec _spec = new(1, ["--provenance", "--linear-issue"], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a || !ProvenanceFlag.TryParse(a.Value("--provenance"), out var claimed) || claimed is not { } provenance)
        {
            return CommandOutput.Usage(Shape);
        }

        var service = services.GetRequiredService<IService<PublishPlanRequest, PublishPlanResponse>>();
        var result = await service.Handle(new PublishPlanRequest(Path.GetFullPath(a[0]), provenance, a.Value("--linear-issue")), cancellationToken).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Error(result.Error) : CommandOutput.Json(result.Value.Plan, json);
    }
}
