using Aw.Meta.Core;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IOwnerStore contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class OwnerStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IOwnerStore> Create();

    /// <summary>Questions are numbered from one and listed in order; answering removes them from the open list.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Questions_are_numbered_and_the_open_list_drops_answered_ones()
    {
        var store = await Create();

        var one = await store.Ask("t1-api", "postgres or sqlite?", A.At(1), _none);
        var two = await store.Ask("t1-ui", "dark mode first?", A.At(2), _none);
        _ = await store.Answer(1, "sqlite", A.At(3), _none);
        var open = await store.List(includeAnswered: false, _none);
        var all = await store.List(includeAnswered: true, _none);

        Expect.That(one.Value == 1 && two.Value == 2, "numbers count from 1");
        Expect.That(open.Value.Select(q => q.Number).SequenceEqual([2]), "only the open question is listed");
        Expect.That(all.Value.Count == 2 && all.Value[0].Answer == "sqlite" && all.Value[0].AnsweredAt == A.At(3), "the answer is stored");
        Expect.That(all.Value[1].AskedBy == "t1-ui" && all.Value[1].Text == "dark mode first?" && all.Value[1].AskedAt == A.At(2), "the question keeps its fields");
    }

    /// <summary>Answers list for relays; a second answer replaces the first; an unknown number is AW-META-006.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Answers_list_for_relays_and_unknown_questions_are_refused()
    {
        var store = await Create();
        _ = await store.Ask("t1-api", "q", A.At(1), _none);
        _ = await store.Answer(1, "first", A.At(2), _none);
        var replaced = await store.Answer(1, "second", A.At(3), _none);
        var missing = await store.Answer(9, "x", A.At(3), _none);
        var answers = await store.Answers(_none);

        Expect.That(replaced.IsSuccess && replaced.Value.Answer == "second", "the answered question is returned");
        Expect.That(answers.Value.SequenceEqual([new OwnerAnswer(1, "t1-api", "second", A.At(3))]), "the answer feeds the relay obligation");
        Expect.That(missing.IsError && missing.Error.Code == MetaErrors.QuestionNotFound.Code, "AW-META-006");
    }
}
