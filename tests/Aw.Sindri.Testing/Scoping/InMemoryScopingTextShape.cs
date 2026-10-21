using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The in-memory <see cref="IScopingTextShape"/>: answers the anchors it was given for any text of a repo, and records what it was asked. A question with no repo, or no words, is refused as the engine refuses it.</summary>
public sealed class InMemoryScopingTextShape : IScopingTextShape
{
    private readonly Result<IReadOnlyList<string>> _answer;
    private readonly List<(RepoName? Repo, string Text)> _asked = [];

    /// <summary>Answers with <paramref name="anchors"/>.</summary>
    /// <param name="anchors">The anchors to suggest.</param>
    public InMemoryScopingTextShape(IReadOnlyList<string> anchors)
    {
        _answer = Results.Success(anchors);
    }

    /// <summary>Answers every question with the error.</summary>
    /// <param name="error">The error.</param>
    public InMemoryScopingTextShape(Error error)
    {
        _answer = error;
    }

    /// <summary>The repo and text of each question, in order.</summary>
    public IReadOnlyList<(RepoName? Repo, string Text)> Asked => _asked;

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> Anchors(RepoName? repo, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        _asked.Add((repo, text));
        if (repo is null)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<string>>>(SindriErrors.InvalidInput.With("the instance's item has no repo"));
        }

        return ValueTask.FromResult(string.IsNullOrWhiteSpace(text) ? SindriErrors.InvalidInput.With("the question needs at least one word") : _answer);
    }
}
