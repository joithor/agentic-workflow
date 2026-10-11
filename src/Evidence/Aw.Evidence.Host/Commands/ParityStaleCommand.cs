using System.Text.Json;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Features.ParityStale;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'parity-stale' command: prints the stale pixel-diff attachments as a JSON array.</summary>
internal static class ParityStaleCommand
{
    internal const string Name = "parity-stale";
    internal const string Shape = "parity-stale --attachments <file.json> --keep id,id";

    private static readonly CliSpec _spec = new(0, ["--attachments", "--keep"], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a || a.PathValue("--attachments") is not { } file || Ids(a.Value("--keep")) is not { } keep)
        {
            return CommandOutput.Usage(Shape);
        }

        var service = services.GetRequiredService<IService<ParityStaleRequest, ParityStaleResponse>>();
        var result = await service.Handle(new ParityStaleRequest(file, keep), cancellationToken).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Error(result.Error) : CommandOutput.Json(result.Value.Stale, json);
    }

    private static List<AttachmentId>? Ids(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var ids = new List<AttachmentId>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!AttachmentId.TryFrom(part, out var id))
            {
                return null;
            }

            ids.Add(id);
        }

        return ids;
    }
}
