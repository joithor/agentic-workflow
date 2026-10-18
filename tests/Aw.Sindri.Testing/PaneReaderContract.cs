using Aw.Sindri.Core.Sessions;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// What every <see cref="IPaneReader"/> keeps: a pane that exists answers its first process and its text, a pane or a socket that doesn't exist answers
/// Gone rather than throwing, and a socket that isn't an absolute path or a pane id that isn't '%n' is Gone without asking anything. The fake and the
/// tmux adapter (against a private server) run it.
/// </summary>
public abstract class PaneReaderContract
{
    /// <summary>The socket path of a server that has <see cref="KnownPane"/>.</summary>
    protected abstract string KnownSocket { get; }

    /// <summary>A pane id ('%n') that exists on <see cref="KnownSocket"/>.</summary>
    protected abstract string KnownPane { get; }

    /// <summary>An absolute socket path nothing listens on.</summary>
    protected abstract string UnknownSocket { get; }

    /// <summary>The port under test.</summary>
    /// <returns>The subject.</returns>
    protected abstract IPaneReader Create();

    [Fact]
    public void Read_of_a_known_pane_captures_its_first_process_and_text()
    {
        var read = Create().Read(KnownSocket, KnownPane, CancellationToken.None);

        Expect.True(read.Kind == PaneReadKind.Captured);
        Expect.True(read.PanePid > 0);
        Expect.True(read.Capture.Length <= PreviewRules.MaxBytes + 1);
    }

    [Fact]
    public void Read_of_an_unknown_pane_is_gone() =>
        Expect.True(Create().Read(KnownSocket, "%987654", CancellationToken.None).Kind == PaneReadKind.Gone);

    [Fact]
    public void Read_of_an_unknown_socket_is_gone_and_never_throws() =>
        Expect.True(Create().Read(UnknownSocket, KnownPane, CancellationToken.None).Kind == PaneReadKind.Gone);

    [Fact]
    public void Read_of_a_relative_socket_or_a_malformed_pane_id_is_gone()
    {
        var subject = Create();

        Expect.True(subject.Read("tmux-socket", KnownPane, CancellationToken.None).Kind == PaneReadKind.Gone);
        Expect.True(subject.Read(KnownSocket, "%", CancellationToken.None).Kind == PaneReadKind.Gone);
        Expect.True(subject.Read(KnownSocket, "main:0.1", CancellationToken.None).Kind == PaneReadKind.Gone);
        Expect.True(subject.Read(KnownSocket, "%1;kill-server", CancellationToken.None).Kind == PaneReadKind.Gone);
    }
}
