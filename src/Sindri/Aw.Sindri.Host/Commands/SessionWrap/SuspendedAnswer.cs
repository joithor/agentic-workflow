namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The body of GET /sindri/launches/{launch}/suspended: '{suspended}'.</summary>
/// <param name="Suspended">True while the launch's run is suspended.</param>
// replaced by RunSuspensionView once #1383 is on main
internal sealed record SuspendedAnswer(bool Suspended);
