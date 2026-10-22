using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Challenges;

/// <summary>
/// Whether an answer is the kind of confirmation its challenge asks for, before it is compared with anything (setup-once spec §4 item 3). Pure; both the process's
/// challenges and the fake use it, so they refuse alike. A refusal here is SND-SETUP-003 and does not use the challenge up: the owner can hold again.
/// </summary>
public static class ChallengeConfirmation
{
    /// <summary>The refusal of <paramref name="answer"/> to <paramref name="issued"/> at <paramref name="now"/>, or null when it is a confirmation of the right kind.</summary>
    /// <param name="issued">The open challenge.</param>
    /// <param name="answer">The answer offered.</param>
    /// <param name="now">The time of the answer.</param>
    /// <returns>SND-SETUP-003 naming what was wrong, or null.</returns>
    public static Error? Refusal(IssuedChallenge issued, ChallengeAnswer answer, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(issued);
        ArgumentNullException.ThrowIfNull(answer);
        var fix = true switch
        {
            _ when answer.HeldMs is not null && !(issued.Channel == ChallengeChannel.App && issued.Mode == ChallengeMode.Held)
                => $"this is a {Kind(issued)} confirmation: type '{issued.Action.Text}' exactly",
            _ when issued.Channel == ChallengeChannel.App && now - issued.IssuedAt < IssuedChallenge.EarliestAnswer
                => "that came too soon after the preview appeared; read it, then confirm again",
            _ when answer.HeldMs is { } held && held < IssuedChallenge.MinHoldMs
                => string.Create(CultureInfo.InvariantCulture, $"hold the button for the full {IssuedChallenge.MinHoldMs / 1000d:0.#} seconds, or type '{issued.Action.Text}'"),
            _ => null,
        };
        return fix is null ? null : SindriErrors.SetupConfirmationRefused.With(fix);
    }

    private static string Kind(IssuedChallenge issued) => issued.Mode == ChallengeMode.Held ? "held" : "typed";
}
