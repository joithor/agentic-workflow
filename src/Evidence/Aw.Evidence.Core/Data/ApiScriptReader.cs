using System.Text.Json;
using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>Reads an API script document and checks its rules.</summary>
public static class ApiScriptReader
{
    /// <summary>Parses and validates a script.</summary>
    /// <param name="utf8Json">The script file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The script, or AW-EVIDENCE-017 naming the JSON location and every problem.</returns>
    public static Result<ApiScript> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) => throw new NotSupportedException();
}
