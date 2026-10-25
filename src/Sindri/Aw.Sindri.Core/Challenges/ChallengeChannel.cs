namespace Aw.Sindri.Core.Challenges;

/// <summary>Where a challenge was issued for, and so where it is answered (setup-once spec §4 item 1). An open challenge is replaced by a new one for the same action on the same channel.</summary>
public enum ChallengeChannel
{
    /// <summary>A human at a terminal: POST /workflow/challenges.</summary>
    Terminal,

    /// <summary>The Sindri app: POST /workflow/challenges/app.</summary>
    App,
}
