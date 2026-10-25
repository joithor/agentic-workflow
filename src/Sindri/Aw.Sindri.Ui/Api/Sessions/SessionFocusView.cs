namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>Whether and how a session's terminal can be brought forward.</summary>
/// <param name="Method">Method, as served.</param>
/// <param name="Possible">Possible, as served.</param>
/// <param name="Why">Why, as served.</param>
internal sealed record SessionFocusView(string Method, bool Possible, string? Why);
