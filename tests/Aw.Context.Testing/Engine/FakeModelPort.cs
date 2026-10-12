using Aw.Context.Core.Models;
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>The in-memory <see cref="IModelPort"/>: answers every request with <see cref="Answer"/>, counting tokens as characters / 4, and records the requests. It passes <see cref="ModelPortContract{TPort}"/>.</summary>
public sealed class FakeModelPort : IModelPort
{
    /// <summary>The text every request is answered with.</summary>
    public string Answer { get; set; } = "fake answer";

    /// <summary>The requests asked so far.</summary>
    public IList<ModelRequest> Requests { get; } = [];

    /// <inheritdoc />
    public ValueTask<Result<ModelAnswer>> Ask(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        var text = Answer.Length / 4 > request.MaxTokens ? Answer[..(request.MaxTokens * 4)] : Answer;
        return ValueTask.FromResult<Result<ModelAnswer>>(new ModelAnswer(text, (request.Prompt.Length + 3) / 4, (text.Length + 3) / 4));
    }
}
