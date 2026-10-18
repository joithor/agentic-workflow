namespace Aw.Sindri.Core.Sessions;

/// <summary>What one tmux server says about itself: its panes and its attached clients.</summary>
/// <param name="Panes">Every pane of every session.</param>
/// <param name="Clients">The attached clients.</param>
public sealed record TmuxServerView(IReadOnlyList<TmuxPane> Panes, IReadOnlyList<TmuxClient> Clients);
