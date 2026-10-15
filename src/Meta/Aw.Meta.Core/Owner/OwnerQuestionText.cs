namespace Aw.Meta.Core.Owner;

/// <summary>Reads the question out of a lane's "OWNER QUESTION" message.</summary>
public static class OwnerQuestionText
{
    private const string Prefix = "OWNER QUESTION";

    /// <summary>Strips the "OWNER QUESTION" lead-in and the separator after it.</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The question; the whole body when it has no lead-in.</returns>
    public static string From(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var trimmed = body.Trim();
        return trimmed.StartsWith(Prefix, StringComparison.Ordinal)
            ? trimmed[Prefix.Length..].TrimStart(' ', ':', '-', '\t').Trim()
            : trimmed;
    }
}
