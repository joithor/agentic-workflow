namespace Aw.Sindri.Tests.Sessions;

/// <summary>One session's host as a golden pins it.</summary>
internal sealed record GoldenHost(string Ref, string Kind, string Label, string? Socket, string? SocketKind, string? Session, string? Pane, string? Terminal, string? TerminalDetail);
