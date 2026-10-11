namespace Aw.BugFix.Core.Model;

/// <summary>A resolution check as asked: the input, the file the helper wrote it to, and that file's SHA-256.</summary>
/// <param name="Input">The input built from state.</param>
/// <param name="InputFile">The written input file (judge-&lt;candidate&gt;.json).</param>
/// <param name="InputSha256">The file's SHA-256: judge's answer must name it, so a verdict can't come from another input.</param>
public sealed record JudgeQuestion(ResolutionCheckInput Input, string InputFile, string InputSha256);
