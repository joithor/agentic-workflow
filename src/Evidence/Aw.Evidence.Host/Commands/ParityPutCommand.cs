using System.Text.Json;
using Aw.Evidence.Features.ParityPut;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'parity-put' command: prints the signed PUT's argv as a JSON array.</summary>
internal static class ParityPutCommand
{
    internal const string Name = "parity-put";
    internal const string Shape = "parity-put --upload <file.json> --file <png>";

    private static readonly CliSpec _spec = new(0, ["--upload", "--file"], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a || a.PathValue("--upload") is not { } upload || a.PathValue("--file") is not { } file)
        {
            return CommandOutput.Usage(Shape);
        }

        var service = services.GetRequiredService<IService<ParityPutRequest, ParityPutResponse>>();
        var result = await service.Handle(new ParityPutRequest(upload, file), cancellationToken).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Error(result.Error) : CommandOutput.Json(result.Value.Argv, json);
    }
}
