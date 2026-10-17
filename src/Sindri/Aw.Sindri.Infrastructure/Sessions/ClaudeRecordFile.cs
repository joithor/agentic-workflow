using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>
/// The fields of a Claude Code session record the collector uses (collector spec §4.1 step 3). The file is undocumented and versioned by Claude Code, so
/// every other member is ignored by the reader: the socket path and the bridge id are never parsed.
/// </summary>
internal sealed record ClaudeRecordFile(int Pid)
{
    public string? SessionId { get; init; }

    public string? Cwd { get; init; }

    public JsonElement ProcStart { get; init; }

    public string? Kind { get; init; }

    public string? Entrypoint { get; init; }

    public string? Tmux { get; init; }

    public string? Name { get; init; }

    public string? Status { get; init; }

    public long? StatusUpdatedAt { get; init; }

    public long? StartedAt { get; init; }
}
