namespace Aw.Sindri.Core.Setup;

/// <summary>A map build that failed once and will try again.</summary>
/// <param name="In">How long until the next try.</param>
/// <param name="Reason">Why the last try didn't finish, in plain words and lower case ("the code index was busy").</param>
public sealed record MapRetry(TimeSpan In, string Reason);
