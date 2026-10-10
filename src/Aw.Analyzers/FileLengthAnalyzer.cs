using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Aw.Analyzers;

/// <summary>
/// AW1005: a source file has at most 300 lines (spec §4b.6), counted like 'wc -l' on a file that ends with a
/// newline. Reported on the first line over the limit. Generated files are skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class FileLengthAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [AwDiagnostics.FileLength];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText(context.CancellationToken);
        var lines = text.Lines.Count;
        if (lines > 1 && text.Lines[lines - 1].Span.IsEmpty)
        {
            lines--;    // the empty "line" after the final newline
        }

        if (lines <= AwDiagnostics.MaxFileLines)
        {
            return;
        }

        var first = text.Lines[AwDiagnostics.MaxFileLines];
        var location = Location.Create(context.Tree, new TextSpan(first.Start, first.Span.Length));
        context.ReportDiagnostic(Diagnostic.Create(AwDiagnostics.FileLength, location, lines, AwDiagnostics.MaxFileLines));
    }
}
