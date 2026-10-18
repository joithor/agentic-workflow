namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>The terminal's last lines.</summary>
/// <param name="Lines">At most 40 lines of at most 200 UTF-16 code units; served as they are, never scrubbed (CA-9) and never rendered as markup.</param>
/// <param name="Truncated">True when a line was cut or the 64 KiB cap dropped output.</param>
/// <param name="CapturedAt">When the read finished.</param>
/// <param name="StartedAt">The session's start time, or null.</param>
/// <param name="Host">The row's host kind and label.</param>
public sealed record SessionPreview(IReadOnlyList<string> Lines, bool Truncated, DateTimeOffset CapturedAt, DateTimeOffset? StartedAt, PreviewHost Host) : GetSessionPreviewResponse;
