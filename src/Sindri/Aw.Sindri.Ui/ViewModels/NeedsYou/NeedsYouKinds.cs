namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The kinds the API serves on a Needs-you row and on its subject, and the queue groups Home counts: the words the app matches on, in one place.</summary>
internal static class NeedsYouKinds
{
    /// <summary>A row's kind: a decision.</summary>
    internal const string Decision = "decision";

    /// <summary>A row's kind: a park (the instance waits on the human).</summary>
    internal const string Park = "park";

    /// <summary>A subject's kind: a service (a failure row).</summary>
    internal const string Service = "service";

    /// <summary>A subject's kind: the profile (a drift row).</summary>
    internal const string Profile = "profile";

    /// <summary>A row's kind: a failure.</summary>
    internal const string Failure = "failure";

    /// <summary>The queue's running group.</summary>
    internal const string Running = "running";

    /// <summary>The queue's with-you group.</summary>
    internal const string WithYou = "withYou";

    /// <summary>The queue's queued group.</summary>
    internal const string Queued = "queued";
}
