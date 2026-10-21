namespace Aw.Sindri.Core.Launches;

/// <summary>The answer of <see cref="ReportedIdAdmission"/>.</summary>
/// <param name="Item">The launch's item id, or null.</param>
/// <param name="Accepted">Whether the id may be recorded.</param>
/// <param name="TimedOut">True when the entry's pattern timed out on the id (Accepted is false then, and this says why).</param>
public sealed record AdmittedLaunch(string? Item, bool Accepted, bool TimedOut = false);
