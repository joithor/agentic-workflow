namespace Aw.Meta.Core.Worktrees;

/// <summary>A live process and the places it holds: its working directory and the files it has open.</summary>
/// <param name="Pid">The process id.</param>
/// <param name="Command">The command line, arguments separated by single spaces.</param>
/// <param name="Paths">The working directory and every open file path.</param>
/// <param name="IsInvoker">Whether it is the scanning process itself or one of its ancestors (the shell that ran it): never evidence that some other command is running.</param>
public sealed record ProcessLocation(int Pid, string Command, IReadOnlyList<string> Paths, bool IsInvoker = false);
