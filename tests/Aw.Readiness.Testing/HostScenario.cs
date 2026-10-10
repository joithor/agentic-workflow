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
}
