using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Owner;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IOwnerStore: the one fake, kept honest by OwnerStoreContract.</summary>
public sealed class InMemoryOwnerStore : IOwnerStore
{
    private readonly List<OwnerQuestion> _questions = [];

    /// <inheritdoc />
    public ValueTask<Result<int>> Ask(string askedBy, string text, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var number = _questions.Count + 1;
        _questions.Add(new OwnerQuestion(number, askedBy, text, at));
        return Done.Of(number);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<OwnerQuestion>>> List(bool includeAnswered, CancellationToken cancellationToken) =>
        Done.Of<IReadOnlyList<OwnerQuestion>>([.. _questions.Where(q => includeAnswered || q.Answer is null)]);

    /// <inheritdoc />
    public ValueTask<Result<OwnerQuestion>> Answer(int number, string text, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var index = _questions.FindIndex(q => q.Number == number);
        if (index < 0)
        {
            return Done.Failed<OwnerQuestion>(MetaErrors.QuestionNotFound.With("no owner question " + number.ToString(CultureInfo.InvariantCulture)));
        }

        _questions[index] = _questions[index] with { Answer = text, AnsweredAt = at };
        return Done.Of(_questions[index]);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<OwnerAnswer>>> Answers(CancellationToken cancellationToken) =>
        Done.Of<IReadOnlyList<OwnerAnswer>>([.. _questions
            .Where(q => q.Answer is not null && q.AnsweredAt is not null)
            .Select(q => new OwnerAnswer(q.Number, q.AskedBy, q.Answer!, q.AnsweredAt!.Value))]); // why: the filter above guarantees both are set
}
