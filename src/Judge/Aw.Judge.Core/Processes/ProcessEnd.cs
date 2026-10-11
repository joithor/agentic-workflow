namespace Aw.Judge.Core.Processes;

/// <summary>How a program run ended.</summary>
public enum ProcessEnd
{
    /// <summary>The program exited by itself; its exit code is set.</summary>
    Exited = 1,

    /// <summary>The program ran past its timeout and was stopped with every process it started.</summary>
    TimedOut = 2,

    /// <summary>The program couldn't be started: not found on the search path, or not executable.</summary>
    NotStarted = 3,
}
