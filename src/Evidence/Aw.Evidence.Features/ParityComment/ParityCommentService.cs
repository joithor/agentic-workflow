using System.Text.Json;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Core.Runs;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ParityComment;

/// <summary>Reads parity.json and renders the Linear or GitHub comment.</summary>
internal sealed class ParityCommentService(IRunFiles files, JsonSerializerOptions json) : IService<ParityCommentRequest, ParityCommentResponse>
{
    public async ValueTask<Result<ParityCommentResponse>> Handle(ParityCommentRequest request, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(RunPath.Combine(request.RunDirectory, ParitySummary.FileName), cancellationToken).ConfigureAwait(false);
        return bytes
            .Bind(b => RunFileReader.Parity(b, json))
            .Map(s => new ParityCommentResponse(request.Target == CommentTarget.GitHub ? ParityComments.GitHub(s) : ParityComments.Linear(s)));
    }
}
