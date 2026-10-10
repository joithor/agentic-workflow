namespace Aw.Judge.Core.Config;

/// <summary>
/// Reads config.json leniently, as the TS judge's loadConfig did: a file that isn't a JSON object, or whose questions
/// block isn't an object, is the default config; unknown provider names and wrong-typed fields are dropped, never fatal.
/// </summary>
public static class JudgeConfigParser
{
    /// <summary>Parses config.json's text.</summary>
    /// <param name="text">The file's text, or null when there is no file.</param>
    /// <returns>The effective config.</returns>
    public static JudgeConfig Parse(string? text) => throw new NotSupportedException("JudgeConfigParser: make JudgeConfigParserTests pass");
}
