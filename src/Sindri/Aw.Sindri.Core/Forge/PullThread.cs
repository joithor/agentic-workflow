namespace Aw.Sindri.Core.Forge;

/// <summary>One item of a pull request's conversation, as the forge port returns it. The body is untrusted text.</summary>
/// <param name="Key">The forge's handle for replying: thread:N, review:N or comment:N.</param>
/// <param name="Kind">'thread', 'review' or 'comment'.</param>
/// <param name="Author">The author's login.</param>
/// <param name="Association">The author's association with the repository (OWNER, MEMBER, CONTRIBUTOR, NONE ...).</param>
/// <param name="IsBot">Whether the author is a bot.</param>
/// <param name="Resolved">Whether the thread is resolved.</param>
/// <param name="Outdated">Whether the code it points at has changed.</param>
/// <param name="Path">The file an inline thread is on.</param>
/// <param name="Body">The opening text.</param>
/// <param name="Markers">The idempotency markers found anywhere in the thread, replies included.</param>
public sealed record PullThread(
    string Key,
    string Kind,
    string Author,
    string Association,
    bool IsBot,
    bool Resolved,
    bool Outdated,
    string? Path,
    string Body,
    IReadOnlyList<string> Markers);
