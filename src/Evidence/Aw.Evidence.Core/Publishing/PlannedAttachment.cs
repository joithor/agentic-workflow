namespace Aw.Evidence.Core.Publishing;

/// <summary>One Linear attachment the agent will upload (after asking and after opening the image).</summary>
/// <param name="Frame">The frame.</param>
/// <param name="File">The side-by-side image (absolute path).</param>
/// <param name="Filename">Its file name.</param>
/// <param name="ContentType">Always image/png.</param>
/// <param name="Size">Its size in bytes.</param>
/// <param name="Title">The attachment title.</param>
/// <param name="Subtitle">The attachment subtitle (at most 500 characters).</param>
/// <param name="Expectation">What the agent must see in the image before uploading it.</param>
public sealed record PlannedAttachment(
    string Frame, string File, string Filename, string ContentType, long Size, string Title, string Subtitle, string Expectation);
