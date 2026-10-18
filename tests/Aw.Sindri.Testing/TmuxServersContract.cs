using Aw.Sindri.Core.Sessions;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// What every <see cref="ITmuxServers"/> keeps: a socket with a server answers its panes (each in a named session, with a '%n' pane id), a socket with none
/// answers an error rather than throwing, and a second read of the same socket answers the same. The fake and the tmux adapter (against a private server) run it.
/// </summary>
public abstract class TmuxServersContract
{
    /// <summary>The socket path of a server that has at least one pane.</summary>
    protected abstract string KnownSocket { get; }

    /// <summary>An absolute socket path nothing listens on.</summary>
    protected abstract string UnknownSocket { get; }

    /// <summary>The port under test.</summary>
    /// <returns>The subject.</returns>
    protected abstract ITmuxServers Create();

    [Fact]
    public void Read_of_a_known_socket_lists_its_panes()
    {
        var view = Create().Read(KnownSocket, CancellationToken.None);

        Expect.True(view.IsSuccess);
        Expect.True(view.Value.Panes.Count > 0);
        Expect.True(view.Value.Panes.All(static p => p.Session.Length > 0 && p.Pane.StartsWith('%') && p.PanePid > 0));
    }

    [Fact]
    public void Read_of_an_unknown_socket_is_an_error_and_never_throws()
    {
        var view = Create().Read(UnknownSocket, CancellationToken.None);

        Expect.True(!view.IsSuccess);
    }

    [Fact]
    public void Read_twice_answers_the_same_panes()
    {
        var subject = Create();

        var first = subject.Read(KnownSocket, CancellationToken.None).Value.Panes;
        var second = subject.Read(KnownSocket, CancellationToken.None).Value.Panes;

        Expect.Sequence(first, second);
    }
}
