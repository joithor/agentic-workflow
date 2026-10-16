using Aw.Context.Core.Models;
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>The in-memory <see cref="IModelPort"/>: answers every request with <see cref="Answer"/>, counting tokens as characters / 4, and records the requests. It passes <see cref="ModelPortContract{TPort}"/>.</summary>
public sealed class FakeModelPort : IModelPort
{
    /// <summary>The text every request is answered with.</summary>
    public string Answer { get; set; } = "fake answer";

    /// <summary>When set, answers each request itself (the tests route on the purpose and the prompt) instead of <see cref="Answer"/>.</summary>
    public Func<ModelRequest, string>? Responder { get; set; }

    /// <summary>The requests asked so far.</summary>
    public IList<ModelRequest> Requests { get; } = [];

    /// <inheritdoc />
    public ValueTask<Result<ModelAnswer>> Ask(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        var answer = Responder?.Invoke(request) ?? Answer;
        var text = answer.Length / 4 > request.MaxTokens ? answer[..(request.MaxTokens * 4)] : answer;
        return ValueTask.FromResult<Result<ModelAnswer>>(new ModelAnswer(text, (request.Prompt.Length + 3) / 4, (text.Length + 3) / 4));
    }
}
