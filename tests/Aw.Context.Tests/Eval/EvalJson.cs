using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Context.Tests.Eval;

/// <summary>AwJson options over the probe types.</summary>
internal static class EvalJson
{
    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(EvalJsonContext.Default);
}
