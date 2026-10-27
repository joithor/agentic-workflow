namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The body of GET /sindri/launches/{launch}/suspended: '{suspended}'.</summary>
/// <param name="Suspended">True while the launch's run is suspended.</param>
internal sealed record SuspendedAnswer(bool Suspended);
