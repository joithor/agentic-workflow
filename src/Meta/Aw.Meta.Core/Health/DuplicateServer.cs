namespace Aw.Meta.Core.Health;

/// <summary>An MCP server command running more times than there are sessions to own it.</summary>
/// <param name="Cmdline">The command line.</param>
/// <param name="Copies">How many copies run.</param>
/// <param name="Allowed">How many were expected (one per Claude session, plus one).</param>
public sealed record DuplicateServer(string Cmdline, int Copies, int Allowed);
