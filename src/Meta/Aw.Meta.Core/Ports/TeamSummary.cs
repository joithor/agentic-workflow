namespace Aw.Meta.Core.Ports;

/// <summary>One team found on disk.</summary>
/// <param name="Name">The team name.</param>
/// <param name="Layout">"named" (teams/&lt;name&gt;) or "legacy" (the flat layout).</param>
/// <param name="Repo">The repo its config names; "(invalid config)" when the config can't be read.</param>
/// <param name="Lanes">The lanes its config names.</param>
/// <param name="MetaSession">The registered meta session's name, else its id; null when no role is registered.</param>
/// <param name="Live">Whether a live Claude session carries the registered role.</param>
/// <param name="Selected">Whether this run acts on this team.</param>
public sealed record TeamSummary(string Name, string Layout, string Repo, IReadOnlyList<string> Lanes, string? MetaSession, bool Live, bool Selected);
