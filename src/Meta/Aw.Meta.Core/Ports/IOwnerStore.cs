using Aw.Kernel;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Owner;

namespace Aw.Meta.Core.Ports;

/// <summary>Questions for the owner and their answers.</summary>
public interface IOwnerStore
{
    /// <summary>Queues a question.</summary>
    /// <param name="askedBy">The asking session's name.</param>
    /// <param name="text">The question.</param>
    /// <param name="at">When it was asked.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The question number, counting from 1.</returns>
    public ValueTask<Result<int>> Ask(string askedBy, string text, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Lists questions in number order.</summary>
    /// <param name="includeAnswered">Whether answered questions are listed too.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The questions.</returns>
    public ValueTask<Result<IReadOnlyList<OwnerQuestion>>> List(bool includeAnswered, CancellationToken cancellationToken);

    /// <summary>Records the owner's answer; a second answer replaces the first.</summary>
    /// <param name="number">The question number.</param>
    /// <param name="text">The answer.</param>
    /// <param name="at">When it was recorded.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answered question, or AW-META-006 for an unknown number.</returns>
    public ValueTask<Result<OwnerQuestion>> Answer(int number, string text, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Every answer, for relay obligations.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answers in number order.</returns>
    public ValueTask<Result<IReadOnlyList<OwnerAnswer>>> Answers(CancellationToken cancellationToken);
}
