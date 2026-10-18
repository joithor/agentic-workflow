namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>The host of a previewed session, as its row says it.</summary>
/// <param name="Kind">'tmux', 'warp', and so on.</param>
/// <param name="Label">The short text a row shows.</param>
public sealed record PreviewHost(string Kind, string Label);
