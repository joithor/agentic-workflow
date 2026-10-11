namespace Aw.Evidence.Core.Locks;

/// <summary>A box-wide lock.</summary>
public enum LockKind
{
    /// <summary>The local QA stack (app, database): one driver at a time.</summary>
    QaStack,

    /// <summary>The heavy-job lock every build, test run and browser session shares.</summary>
    HeavyJob,
}
