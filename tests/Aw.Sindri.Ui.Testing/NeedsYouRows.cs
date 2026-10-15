using System.Globalization;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Testing;

/// <summary>Builders for needs-you rows in the shapes the screens fixture gives (a park, a decision, a failure, a profile drift).</summary>
internal static class NeedsYouRows
{
    internal const string Command = "sindri workflow answer ITM-01JC8 --grant 1";

    /// <summary>A parked item: a protected primary and its command.</summary>
    internal static NeedsYouEntry Park(string id = "park:ITM-01JC8", int minutes = 12) => new(id, NeedsYouUrgency.Pulse)
    {
        Chip = new NeedsYouChip("PARKED", NeedsYouChipTone.Attn),
        ShownId = "ITM-01JC8",
        Title = "Retry storm in outbox dispatcher",
        Repo = "agentic-workflow",
        Reason = "checks failed twice with the same findings",
        Since = A.Noon.AddMinutes(-minutes),
        PrimaryAction = "GrantRounds",
        Actions = [new NeedsYouAction("GrantRounds", "Grant rounds", ActionReadiness.CliOnly, Command, RejectLike: false)],
        Subject = new NeedsYouSubject("item", "ITM-01JC8"),
        ItemId = "ITM-01JC8",
        Cli = Command,
    };

    /// <summary>A decision: Accept (recommended, first) and Decline (rejectLike).</summary>
    internal static NeedsYouEntry Decision(string id = "decision:DEC-1", string? item = "ITM-01JD2") => new(id, NeedsYouUrgency.Pulse)
    {
        Chip = new NeedsYouChip("DECISION", NeedsYouChipTone.Attn),
        ShownId = "REQ-01JD4",
        Title = "Re-scope: drop the iOS head from this item",
        Repo = "sindri-app",
        Reason = "decision · re-scope",
        Since = A.Noon.AddMinutes(-26),
        PrimaryAction = "Decide",
        Actions =
        [
            new NeedsYouAction("Decide", "Accept", ActionReadiness.Ready, "sindri decide DEC-1 accept", RejectLike: false) { OptionId = "accept" },
            new NeedsYouAction("Decide", "Decline", ActionReadiness.Ready, "sindri decide DEC-1 decline", RejectLike: true) { OptionId = "decline" },
        ],
        Subject = new NeedsYouSubject("decision", "DEC-1"),
        ItemId = item,
        Cli = "sindri decide DEC-1 <choice>",
    };

    /// <summary>A failed service: RunService and PauseService are not available yet.</summary>
    internal static NeedsYouEntry Failure(string id = "failure:service:index") => new(id, NeedsYouUrgency.Colour)
    {
        Chip = new NeedsYouChip("FAILED", NeedsYouChipTone.Fail),
        Title = "Nightly index build",
        Reason = "failure · index",
        Since = A.Noon.AddHours(-9),
        PrimaryAction = "RunService",
        Actions =
        [
            new NeedsYouAction("RunService", "Run again", ActionReadiness.Pending, "sindri services run index", RejectLike: false),
            new NeedsYouAction("PauseService", "Pause", ActionReadiness.Pending, "sindri services pause index", RejectLike: false),
        ],
        Subject = new NeedsYouSubject("service", "index"),
        Cli = "sindri services run index",
    };

    /// <summary>A drifted profile: calm, a protected approve, and the subject optional.</summary>
    internal static NeedsYouEntry Profile(NeedsYouSubject? subject = null) => new("profile:drift", NeedsYouUrgency.Calm)
    {
        Chip = new NeedsYouChip("PROFILE", NeedsYouChipTone.Attn),
        Title = "Profile changed since approval · 2 keys",
        Reason = "profile · drifted",
        Since = A.Noon.AddHours(-2),
        PrimaryAction = "ApproveProfile",
        Actions = [new NeedsYouAction("ApproveProfile", "Approve", ActionReadiness.CliOnly, "sindri profile approve 3f1c09", RejectLike: false)],
        Subject = subject,
        Cli = "sindri profile approve 3f1c09",
    };

    /// <summary>A pulse row with just an id, for lists that need many.</summary>
    internal static NeedsYouEntry Many(int n)
    {
        var shown = string.Create(CultureInfo.InvariantCulture, $"ITM-{n:D5}");
        return Park("park:" + shown) with { ShownId = shown };
    }
}
