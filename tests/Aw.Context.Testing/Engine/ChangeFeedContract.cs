using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="IChangeFeed"/> honours (spec §5.1): pages within the limit, a replay from the same
/// cursor answers the same page (idempotent), and paging from the start reaches every change and then an empty page.
/// A binding creates a feed holding exactly <see cref="Count"/> changes.
/// </summary>
/// <typeparam name="TFeed">The implementation.</typeparam>
public abstract class ChangeFeedContract<TFeed>
    where TFeed : IChangeFeed
{
    /// <summary>How many changes the bound feed holds.</summary>
    protected const int Count = 5;

    /// <summary>A fresh feed with <see cref="Count"/> changes.</summary>
    /// <returns>The feed.</returns>
    protected abstract TFeed Create();

    /// <summary>Paging reaches every change once, then an empty page; a replay repeats its page.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Paging_reaches_every_change_and_a_replay_repeats_its_page()
    {
        var feed = Create();
        var seen = new List<SourceRef>();
        SourceCursor? cursor = null;
        for (var pages = 0; pages < Count + 2; pages++)
        {
            var page = Expect(await feed.Changes(cursor, 2, CancellationToken.None));
            var replay = Expect(await feed.Changes(cursor, 2, CancellationToken.None));
            Check(page.Changed.Count <= 2, "a page stays within its limit");
            Check(page.Changed.SequenceEqual(replay.Changed) && page.Next == replay.Next, "a replay from the same cursor answers the same page");
            if (page.Changed.Count == 0)
            {
                break;
            }

            seen.AddRange(page.Changed);
            cursor = page.Next;
        }

        Check(seen.Count == Count && seen.Distinct().Take(Count + 1).Count() == Count, "paging reaches every change exactly once");
    }

    private static T Expect<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"{typeof(TFeed).Name}: {result.Error.Code} {result.Error.Message}");

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TFeed).Name}: {what}");
        }
    }
}
