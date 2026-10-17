using System.Diagnostics.CodeAnalysis;
using Aw.Redaction;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'warp' viewer (API P10a, unit U6): a per-launch launch configuration opened through warp://launch.</summary>
[SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Registered in U7 (API P10a).")]
internal sealed class WarpViewer(WarpViewerOptions options, ISessionHost host, TimeProvider time, IRedactor? redactor = null) : ITerminalViewer
{
    public string Kind => "warp";

    public ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException($"{options}{host}{time}{redactor}");

    public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Deletes launch files older than 60 s (the launcher service's start-up sweep).</summary>
    public void Sweep() => throw new NotSupportedException();
}
