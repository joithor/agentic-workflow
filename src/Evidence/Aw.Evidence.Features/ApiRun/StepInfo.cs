using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Who a step is: its 1-based position, target text, action and the names of the headers it sent.</summary>
/// <param name="Index">The position.</param>
/// <param name="Target">'GET /path', or the program and its words.</param>
/// <param name="Action">Request or CLI call.</param>
/// <param name="HeaderNames">Header names sent (never values).</param>
internal sealed record StepInfo(int Index, string Target, ApiAction Action, IReadOnlyList<string> HeaderNames);
