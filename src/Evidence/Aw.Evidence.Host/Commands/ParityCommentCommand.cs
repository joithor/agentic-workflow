using Aw.Evidence.Features.ParityComment;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'parity-comment' command: prints the Linear or GitHub comment as markdown.</summary>
internal static class ParityCommentCommand
{
    internal const string Name = "parity-comment";
    internal const string Shape = "parity-comment <run-dir> --for linear|github";

    private static readonly CliSpec _spec = new(1, ["--for"], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a || Target(a.Value("--for")) is not { } chosen)
        {
            return CommandOutput.Usage(Shape);
        }

        var service = services.GetRequiredService<IService<ParityCommentRequest, ParityCommentResponse>>();
        var result = await service.Handle(new ParityCommentRequest(Path.GetFullPath(a[0]), chosen), cancellationToken).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Error(result.Error) : CommandOutput.Markdown(result.Value.Markdown);
    }

    private static CommentTarget? Target(string? text) => text switch
    {
        "linear" => CommentTarget.Linear,
        "github" => CommentTarget.GitHub,
        _ => null,
    };
}
