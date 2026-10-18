using Aw.Sindri.Core.Sessions;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// What every <see cref="ITerminalNames"/> keeps: each row has a name, no name twice, a second read answers the same, and an unreadable source answers no rows
/// rather than throwing. The fake and the terminals.json adapter run it.
/// </summary>
public abstract class TerminalNamesContract
{
    /// <summary>A source with at least one terminal.</summary>
    /// <returns>The subject.</returns>
    protected abstract ITerminalNames Create();

    /// <summary>A source with no terminal.</summary>
    /// <returns>The subject.</returns>
    protected abstract ITerminalNames CreateEmpty();

    [Fact]
    public void All_lists_each_terminal_once_by_a_non_empty_name()
    {
        var names = Create().All.Select(static t => t.Name).ToList();

        Expect.True(names.Count > 0);
        Expect.True(names.TrueForAll(static n => n.Length > 0));
        Expect.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void All_twice_answers_the_same_rows()
    {
        var subject = Create();

        Expect.Sequence(subject.All.Select(static t => t.Name), subject.All.Select(static t => t.Name));
    }

    [Fact]
    public void All_without_a_source_is_empty_and_never_throws() =>
        Expect.Equal(0, CreateEmpty().All.Count);
}
