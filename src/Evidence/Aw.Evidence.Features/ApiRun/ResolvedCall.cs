using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>A CLI call that passed its guards: the real program path, its arguments, the text its step shows and the golden file's text.</summary>
/// <param name="Step">The call.</param>
/// <param name="Executable">The real path of the program.</param>
/// <param name="Target">The step target (the program and its words).</param>
/// <param name="GoldenText">The golden file's text, when the expectation names one.</param>
internal sealed record ResolvedCall(CliStep Step, string Executable, string Target, string? GoldenText);
