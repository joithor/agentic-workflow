namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>There is nothing to show right now.</summary>
/// <param name="Reason">'not-in-tmux', 'not-live' or 'timed-out' (the PreviewReason constants).</param>
/// <param name="CapturedAt">When the answer was made.</param>
public sealed record SessionPreviewUnavailable(string Reason, DateTimeOffset CapturedAt) : GetSessionPreviewResponse;
