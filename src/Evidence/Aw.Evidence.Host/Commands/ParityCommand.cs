using System.Text.Json;
using Aw.Evidence.Features.Parity;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'parity' command: binds its arguments, calls Parity and prints the summary (exit 2 when a geometry check failed).</summary>
internal static class ParityCommand
{
    internal const string Name = "parity";
    internal const string Shape =
        "parity <manifest.json> <run-dir> [--host url] [--allow-preview-host] [--allow-writes] [--app-build sha] " +
        "[--db-provenance seeded|unknown] [--lock-timeout s]";

    private static readonly CliSpec _spec = new(
        2,
        ["--host", "--app-build", "--db-provenance", CliArguments.LockTimeoutFlag],
        ["--allow-preview-host", "--allow-writes"]);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a || a.LockTimeout() is not { } timeout
            || !ProvenanceFlag.TryParse(a.Value("--db-provenance"), out var database))
        {
            return CommandOutput.Usage(Shape);
        }

        var request = new ParityRequest(
            Path.GetFullPath(a[0]), Path.GetFullPath(a[1]), timeout, a.Value("--host"), a.Has("--allow-preview-host"), a.Has("--allow-writes"),
            a.Value("--app-build"), database);
        var service = services.GetRequiredService<IService<ParityRequest, ParityResponse>>();
        var result = await service.Handle(request, cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Error(result.Error);
        }

        foreach (var failure in result.Value.GeometryFailures)
        {
            CommandOutput.Message($"geometry: {failure}");
        }

        return CommandOutput.Json(result.Value.Summary, json, result.Value.Passed ? CommandOutput.Ok : CommandOutput.CheckFailed);
    }
}
