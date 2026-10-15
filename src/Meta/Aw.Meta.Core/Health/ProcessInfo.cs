namespace Aw.Meta.Core.Health;

/// <summary>A running process.</summary>
/// <param name="Pid">The process id.</param>
/// <param name="Ppid">The parent's process id.</param>
/// <param name="Cmdline">The command line with arguments separated by single spaces.</param>
/// <param name="RssKb">Resident memory in kilobytes.</param>
/// <param name="AgeSeconds">Seconds since it started.</param>
public sealed record ProcessInfo(int Pid, int Ppid, string Cmdline, long RssKb, long AgeSeconds);
