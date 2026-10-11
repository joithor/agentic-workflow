using System.Text.Json;
using Aw.Kernel;

namespace Aw.Evidence.Core.Runs;

/// <summary>Reads run files written by this build; another schema is refused.</summary>
public static class RunFileReader
{
    /// <summary>Reads summary.json.</summary>
    /// <param name="utf8Json">The file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The summary, or AW-EVIDENCE-001.</returns>
    public static Result<RunSummary> Summary(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) =>
        JsonInput.Read<RunSummary>(utf8Json, options, EvidenceErrors.Invalid)
            .Bind(static s => s.Schema == RunSummary.SchemaName ? Results.Success(s) : WrongSchema<RunSummary>(s.Schema, RunSummary.SchemaName));

    /// <summary>Reads parity.json.</summary>
    /// <param name="utf8Json">The file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The summary, or AW-EVIDENCE-001.</returns>
    public static Result<ParitySummary> Parity(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) =>
        JsonInput.Read<ParitySummary>(utf8Json, options, EvidenceErrors.Invalid)
            .Bind(static s => s.Schema == ParitySummary.SchemaName ? Results.Success(s) : WrongSchema<ParitySummary>(s.Schema, ParitySummary.SchemaName));

    private static Result<T> WrongSchema<T>(string found, string expected) =>
        EvidenceErrors.Invalid.With($"schema '{found}' is not '{expected}'");
}
