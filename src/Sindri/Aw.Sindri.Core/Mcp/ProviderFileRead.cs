namespace Aw.Sindri.Core.Mcp;

/// <summary>One provider file as read: its path for messages, and its text, or why it couldn't be read.</summary>
/// <param name="File">Which provider's file.</param>
/// <param name="Path">Its path, shown with '~' for the home directory.</param>
/// <param name="Text">Its text, or null when it doesn't exist or couldn't be read.</param>
/// <param name="Problem">Why it couldn't be read (never its content), or null.</param>
public sealed record ProviderFileRead(ProviderFile File, string Path, string? Text, string? Problem);
