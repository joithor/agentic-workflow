namespace Aw.Sindri.Core.NeedsYou;

/// <summary>The values of <see cref="NeedsYouOpen.Kind"/>: lowercase, one per place a needs-you row sends the human.</summary>
public static class NeedsYouOpenKinds
{
    /// <summary>A workflow instance (a park, a parked descendant, an idle takeover).</summary>
    public const string Instance = "instance";

    /// <summary>An instance's desk, where its open question is answered.</summary>
    public const string Questions = "questions";

    /// <summary>A pending decision.</summary>
    public const string Decision = "decision";

    /// <summary>A service (a failed one).</summary>
    public const string Service = "service";

    /// <summary>A job.</summary>
    public const string Job = "job";

    /// <summary>The profile.</summary>
    public const string Profile = "profile";

    /// <summary>A workflow recipe awaiting approval.</summary>
    public const string Recipe = "recipe";

    /// <summary>A setup step.</summary>
    public const string Setup = "setup";
}
