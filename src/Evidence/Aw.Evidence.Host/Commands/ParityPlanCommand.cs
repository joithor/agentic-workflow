using System.Text.Json;
using Aw.Evidence.Features.ParityPlan;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'parity-plan' command: prints the attachment plan; exit 3 while an image is unreviewed.</summary>
internal static class ParityPlanCommand
{
    internal const string Name = "parity-plan";
    internal const string Shape = "parity-plan <run-dir> [--reviewed file,file]";

    private static readonly CliSpec _spec = new(1, ["--reviewed"], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a)
        {
            return CommandOutput.Usage(Shape);
        }

        string[] reviewed = [.. (a.Value("--reviewed") ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Path.GetFullPath)];
        var service = services.GetRequiredService<IService<ParityPlanRequest, ParityPlanResponse>>();
        var result = await service.Handle(new ParityPlanRequest(Path.GetFullPath(a[0]), reviewed), cancellationToken).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Error(result.Error)
            : CommandOutput.Json(result.Value, json, result.Value.ReviewBlocked ? CommandOutput.ReviewBlocked : CommandOutput.Ok);
    }
}
