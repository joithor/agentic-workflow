namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Where <see cref="TmuxSessionHost"/> finds its tmux (API P10a, U5): the defaults are the product's; a test points them at a private server.</summary>
/// <param name="Executable">The tmux program ('tmux' on the login PATH).</param>
/// <param name="Socket">The server's socket name ('-L'; 'sindri').</param>
/// <param name="TmuxTmpDir">The directory tmux keeps its sockets in (TMUX_TMPDIR), or null for tmux's own default.</param>
internal sealed record TmuxHostOptions(string Executable = "tmux", string Socket = "sindri", string? TmuxTmpDir = null);
