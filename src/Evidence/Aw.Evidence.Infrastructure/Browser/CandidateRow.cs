namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>One visible candidate as the page script returns it.</summary>
internal sealed class CandidateRow
{
    public int Index { get; set; }

    public string? Role { get; set; }

    public string? AccessibleName { get; set; }

    public string? TestId { get; set; }

    public string? Text { get; set; }
}
