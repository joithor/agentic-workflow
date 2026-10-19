using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>An <see cref="IScopingTextShape"/> that answers one canned JSON text or error and records what it was asked.</summary>
internal sealed class FakeTextShape : IScopingTextShape
{
    private readonly Result<string> _answer;

    /// <summary>Answers with the 'contextQuery' fact's text.</summary>
    /// <param name="answer">The JSON text.</param>
    public FakeTextShape(string answer)
    {
        _answer = answer;
    }

    /// <summary>Answers with the error.</summary>
    /// <param name="error">The error.</param>
    public FakeTextShape(Error error)
    {
        _answer = error;
    }

    /// <summary>The repo and text of each question, in order.</summary>
    public List<(RepoName? Repo, string Text)> Asked { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<string>> Anchors(RepoName? repo, string text, CancellationToken cancellationToken)
    {
        Asked.Add((repo, text));
        return ValueTask.FromResult(_answer);
    }
}
