namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>The idle sweep's decision for one Step session. Pure: the time comes in through <see cref="IdleInputs.Now"/>.</summary>
public static class IdleRules
{
    /// <summary>Decides what to do with the session.</summary>
    /// <param name="inputs">What is known about it.</param>
    /// <param name="stalledAfter">How long a session may sit idle (or after the nudge) before it is nudged or ended.</param>
    /// <returns>The action.</returns>
    public static IdleAction Decide(IdleInputs inputs, TimeSpan stalledAfter) => throw new NotSupportedException();
}
