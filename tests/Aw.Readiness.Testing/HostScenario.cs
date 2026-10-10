namespace Aw.Readiness.Testing;

/// <summary>What the repo host answers in a <see cref="RepoHostContract"/> test.</summary>
public enum HostScenario
{
    /// <summary>main is protected, secret scanning and push protection are on, 3 releases in the last 90 days.</summary>
    Ready = 1,

    /// <summary>main is not protected, secret scanning is off, no releases.</summary>
    Bare = 2,

    /// <summary>The host can't be reached at all.</summary>
    Unreachable = 3,

    /// <summary>Like Ready, but main is protected by ruleset rules instead of branch protection.</summary>
    Rulesets = 4,

    /// <summary>Like Bare, but the plan can't protect branches at all (HTTP 403 "Upgrade to GitHub Pro").</summary>
    PlanCannotProtect = 5,

    /// <summary>Like Ready, but the login isn't a repo admin: GitHub answers the protection endpoint with a plain 404
    /// and doesn't report secret scanning, so neither can be known.</summary>
    NotAdmin = 6,

    /// <summary>Like Bare, but main has a ruleset whose only rule protects nothing (copilot_code_review).</summary>
    WeakRuleset = 7,
}
