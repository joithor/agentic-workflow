using System.Globalization;
using System.Text.Json;
using Aw.Evidence.Features.GhPublish;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>
/// The 'gh-publish' command: pushes a run's images to the PR's evidence branch and writes pr-comment.md. Exit 0 pushed,
/// 1 bad usage or input or provenance not seeded (nothing pushed), 3 an image is not in --reviewed (nothing pushed), 4 gh failed.
/// </summary>
internal static class GhPublishCommand
{
    internal const string Name = "gh-publish";
    internal const string Shape = "gh-publish <run-dir> --repo owner/name --pr N [--provenance seeded|unknown] [--reviewed file,file]";

    private static readonly CliSpec _spec = new(1, ["--repo", "--pr", "--provenance", "--reviewed"], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a || a.Value("--repo") is not { } repo || PullRequest(a.Value("--pr")) is not { } pr
            || !ProvenanceFlag.TryParse(a.Value("--provenance"), out var provenance))
        {
            return CommandOutput.Usage(Shape);
        }

        string[] reviewed = [.. (a.Value("--reviewed") ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Path.GetFullPath)];
        var service = services.GetRequiredService<IService<GhPublishRequest, GhPublishResponse>>();
        var result = await service.Handle(new GhPublishRequest(Path.GetFullPath(a[0]), repo, pr, provenance, reviewed), cancellationToken).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Error(result.Error)
            : CommandOutput.Json(result.Value, json, result.Value.ReviewBlocked ? CommandOutput.ReviewBlocked : CommandOutput.Ok);
    }

    private static int? PullRequest(string? text) =>
        text is not null && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var pr) && pr >= 1 ? pr : null;
}
