using Aw.Sindri.Core.Sessions;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// What every <see cref="IUnixSockets"/> keeps: each bound socket maps its inode to a non-empty path, a second read answers the same, and a source that can't
/// be read answers an empty map rather than throwing. The fake and the /proc adapter run it.
/// </summary>
public abstract class UnixSocketsContract
{
    /// <summary>A table with at least one bound socket.</summary>
    /// <returns>The subject.</returns>
    protected abstract IUnixSockets Create();

    /// <summary>A table whose source is missing.</summary>
    /// <returns>The subject.</returns>
    protected abstract IUnixSockets CreateWithoutSource();

    [Fact]
    public void Read_maps_each_inode_to_a_non_empty_path()
    {
        var sockets = Create().Read();

        Expect.True(sockets.Count > 0);
        Expect.True(sockets.Values.All(static p => p.Length > 0));
    }

    [Fact]
    public void Read_twice_answers_the_same_map()
    {
        var subject = Create();

        Expect.Sequence(subject.Read().OrderBy(static p => p.Key), subject.Read().OrderBy(static p => p.Key));
    }

    [Fact]
    public void Read_without_a_source_is_empty_and_never_throws() =>
        Expect.Equal(0, CreateWithoutSource().Read().Count);
}
