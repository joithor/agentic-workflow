using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers.Tests;

/// <summary>
/// Runs one analyzer over an in-memory compilation built against the running runtime's assemblies (no network, no
/// reference packs), and fails the test if the sample itself doesn't compile, so a typo can't pass as "no diagnostic".
/// </summary>
internal static class AnalyzerHarness
{
    private static readonly ImmutableArray<MetadataReference> _references =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!) // why: always set by the .NET host
            .Split(Path.PathSeparator)
            .Where(p => p.Length > 0)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))];

    /// <summary>The analyzer's diagnostics as "ID@line" (1-based), sorted.</summary>
    public static async Task<IReadOnlyList<string>> Run(
        DiagnosticAnalyzer analyzer, string assemblyName, params (string Path, string Source)[] files)
    {
        var diagnostics = await Diagnostics(analyzer, assemblyName, files);
        return [.. diagnostics
            .Select(d => d.Id + "@" + (d.Location.GetLineSpan().StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>The analyzer's diagnostics for one file named Sample.cs.</summary>
    public static Task<IReadOnlyList<string>> Run(DiagnosticAnalyzer analyzer, string assemblyName, string source) =>
        Run(analyzer, assemblyName, ("Sample.cs", source));

    /// <summary>The analyzer's raw diagnostics, for message checks.</summary>
    public static async Task<ImmutableArray<Diagnostic>> Diagnostics(
        DiagnosticAnalyzer analyzer, string assemblyName, params (string Path, string Source)[] files)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var ct = TestContext.Current.CancellationToken;
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Source, parse, f.Path, cancellationToken: ct));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var errors = compilation.GetDiagnostics(ct).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, "sample does not compile: " + string.Join("; ", errors));
        return await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(ct);
    }
}
