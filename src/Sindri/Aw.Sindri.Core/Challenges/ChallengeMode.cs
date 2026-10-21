namespace Aw.Sindri.Core.Challenges;

/// <summary>How a challenge is confirmed (setup-once spec §4 item 3).</summary>
public enum ChallengeMode
{
    /// <summary>The action text is typed back exactly, as the terminal's is.</summary>
    Typed,

    /// <summary>A button is held for <see cref="IssuedChallenge.MinHoldMs"/>, or the action text is typed instead.</summary>
    Held,
}
