using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>The options every eval file store uses, over <see cref="EvalFilesJsonContext"/>.</summary>
internal static class EvalFileJson
{
    /// <summary>The options.</summary>
    internal static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(EvalFilesJsonContext.Default);
}
