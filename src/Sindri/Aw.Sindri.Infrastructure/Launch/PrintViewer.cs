using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'print' viewer (API P10a, unit U6): nothing opens; the attach command travels on 'launch.started' and the app or 'sindri open' prints it.</summary>
[SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Registered in U7 (API P10a).")]
internal sealed class PrintViewer(string source = "file") : ITerminalViewer
{
    public string Kind => "print";

    public ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException(source);

    public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
}
