using System.Globalization;
using System.Text.Json;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Transcripts;
using Aw.Kernel;

namespace Aw.Judge.Infrastructure.Transcripts;

/// <summary>
/// ITranscriptReader over the Claude transcripts under ~/.claude/projects (&lt;projects&gt;/&lt;project&gt;/&lt;session&gt;.jsonl). A
/// transcript is untrusted data: it is parsed, never followed, and a line that isn't a JSON object is skipped.
/// </summary>
/// <param name="projectsDirectory">The projects directory.</param>
internal sealed class ClaudeTranscriptReader(string projectsDirectory) : ITranscriptReader
{
    private const string UnknownSession = "unknown";

    /// <inheritdoc />
    public ValueTask<Result<Transcript>> FindSession(SessionId session, CancellationToken cancellationToken)
    {
        var name = session.Value;
        if (name.Length == 0 || name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal) || name is "." or "..")
        {
            return Missing($"no transcript for session '{name}'");
        }

        var file = Candidates(name).FirstOrDefault(File.Exists);
        return file is null ? Missing($"no transcript for session '{name}'") : Read(file, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Transcript>> Read(string path, CancellationToken cancellationToken)
    {
        string text;
        DateTimeOffset modifiedAt;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return JudgeErrors.NoTranscript.With($"can't read the transcript '{path}': {exception.Message}");
        }

        return Parse(text, modifiedAt);
    }

    private static Result<Transcript> Parse(string text, DateTimeOffset modifiedAt)
    {
        var prompts = new List<TranscriptPrompt>();
        var turns = new List<TurnBuilder>();
        var ends = new Dictionary<SessionId, DateTimeOffset>();
        var current = new Dictionary<string, TurnBuilder>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            if (line.AsSpan().Trim().IsEmpty)
            {
                continue;
            }

            using var document = ParseRow(line);
            if (document is not null)
            {
                var row = document.RootElement;
                var sessionName = Text(row, "sessionId") is { Length: > 0 and <= 4096 } id ? id : UnknownSession;
                var at = Timestamp(row);
                if (at is { } stamp)
                {
                    var key = SessionId.From(sessionName);
                    ends[key] = ends.TryGetValue(key, out var seen) && seen > stamp ? seen : stamp;
                }

                switch (Text(row, "type"))
                {
                    case "user" when PromptText(row) is { } prompt && TurnOutcomes.IsRealPromptText(prompt):
                        if (at is { } sent)
                        {
                            prompts.Add(new TranscriptPrompt(sent, prompt));
                        }

                        if (prompt.AsSpan().Trim().IsEmpty)
                        {
                            break;
                        }

                        var index = counts.GetValueOrDefault(sessionName);
                        counts[sessionName] = index + 1;
                        var turn = new TurnBuilder(SessionId.From(sessionName), index, prompt);
                        current[sessionName] = turn;
                        turns.Add(turn);
                        break;
                    case "assistant" when current.TryGetValue(sessionName, out var open):
                        AddEdits(row, open.Edits);
                        break;
                    default:
                        break;
                }
            }
        }

        return new Transcript(modifiedAt, prompts, [.. turns.Select(t => new TranscriptTurn(t.Session, t.Index, t.Prompt, t.Edits))], ends);
    }

    // The line as a JSON object, or null when it is not one.
    private static JsonDocument? ParseRow(string line)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            return document;
        }

        document.Dispose();
        return null;
    }

    // The text of a user message, or null for a tool result or a content shape that carries no text.
    private static string? PromptText(JsonElement row)
    {
        if (!row.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var content))
        {
            return null;
        }

        switch (content.ValueKind)
        {
            case JsonValueKind.String:
                return content.GetString();
            case JsonValueKind.Array:
                var blocks = content.EnumerateArray().Where(b => b.ValueKind == JsonValueKind.Object).ToList();
                if (blocks.Exists(b => Text(b, "type") == "tool_result"))
                {
                    return null;
                }

                var texts = blocks.Where(b => Text(b, "type") == "text").Select(b => Text(b, "text") ?? "").ToList();
                return texts.Count == 0 ? null : string.Join('\n', texts);
            default:
                return null;
        }
    }

    private static void AddEdits(JsonElement row, List<TranscriptEdit> edits)
    {
        if (!row.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var block in content.EnumerateArray().Where(b => b.ValueKind == JsonValueKind.Object && Text(b, "type") == "tool_use"))
        {
            if (!block.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var file = Text(input, "file_path") ?? "";
            switch (Text(block, "name"))
            {
                case "Edit":
                    edits.Add(new TranscriptEdit(file, Text(input, "old_string") ?? "", Text(input, "new_string") ?? ""));
                    break;
                case "Write":
                    edits.Add(new TranscriptEdit(file, "", Text(input, "content") ?? ""));
                    break;
                case "MultiEdit" when input.TryGetProperty("edits", out var list) && list.ValueKind == JsonValueKind.Array:
                    edits.AddRange(list.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.Object)
                        .Select(e => new TranscriptEdit(file, Text(e, "old_string") ?? "", Text(e, "new_string") ?? "")));
                    break;
                default:
                    break;
            }
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Timestamp(JsonElement row) =>
        Text(row, "timestamp") is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : null;

    private static ValueTask<Result<Transcript>> Missing(string detail) =>
        ValueTask.FromResult<Result<Transcript>>(JudgeErrors.NoTranscript.With(detail));

    // Fails open: a missing or unreadable projects directory yields no candidates.
    private IEnumerable<string> Candidates(string name)
    {
        string[] directories;
        try
        {
            directories = Directory.GetDirectories(projectsDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return directories.Select(directory => Path.Combine(directory, name + ".jsonl"));
    }

    private sealed class TurnBuilder(SessionId session, int index, string prompt)
    {
        public SessionId Session { get; } = session;

        public int Index { get; } = index;

        public string Prompt { get; } = prompt;

        public List<TranscriptEdit> Edits { get; } = [];
    }
}
