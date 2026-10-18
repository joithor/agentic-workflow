namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>The pure half of the registration: given a provider's current file text, what the file should say.</summary>
internal static class ProviderPlans
{
    /// <summary>The state of Claude Code's local-scope entry.</summary>
    internal enum ClaudeState { Absent, Same, Different }

    /// <summary>A file's planned text: <c>Text</c> null with <c>Problem</c> null is unchanged; a problem means the file can't be edited safely.</summary>
    internal sealed record FilePlan(string? Text, string? Problem);

    internal static ClaudeState Claude(string? claudeJson, string checkout, string url, string repo) => throw new NotSupportedException();

    internal static FilePlan Cursor(string? existing, string url, string repo) => throw new NotSupportedException();

    internal static FilePlan Codex(string? existing, string url, string repo) => throw new NotSupportedException();

    internal static bool CodexTrusted(string? userConfig, string checkout) => throw new NotSupportedException();

    internal static string CodexTrustRow(string? userConfig, string checkout) => throw new NotSupportedException();
}
