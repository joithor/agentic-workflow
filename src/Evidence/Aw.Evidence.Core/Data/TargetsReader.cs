using System.Text.Json;
using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>Reads a targets config document and checks its rules.</summary>
public static class TargetsReader
{
    /// <summary>Parses and validates a targets file.</summary>
    /// <param name="utf8Json">The file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The targets, or AW-EVIDENCE-022 naming the JSON location and every problem.</returns>
    public static Result<ResolvedTargets> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) => throw new NotSupportedException();
}
