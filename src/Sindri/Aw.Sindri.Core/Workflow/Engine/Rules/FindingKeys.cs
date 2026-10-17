namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The keys of a report's findings (engine P8 D10, A §17.23.13): a 'fact:findings' value that is a JSON list of
/// {file, check, message} gives one key per finding, the digest of its normalized 'file|check|message', so a later round that
/// still has all of them (and perhaps more) made no progress. Any other shape has no keys and keeps the whole-value digest rule.
/// </summary>
public static class FindingKeys
{
    /// <summary>The sorted, distinct keys of <paramref name="findings"/>.</summary>
    /// <param name="findings">The 'fact:findings' value as stored (UTF-8 JSON).</param>
    /// <returns>The keys; null when the value isn't a list of {file, check, message} objects.</returns>
    public static IReadOnlyList<string>? Of(ReadOnlySpan<byte> findings) => throw new NotSupportedException("FindingKeys.Of is not built yet");
}
