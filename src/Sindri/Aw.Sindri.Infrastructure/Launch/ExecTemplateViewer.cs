using System.Diagnostics.CodeAnalysis;
using Aw.Redaction;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'exec-template' viewer (API P10a, unit U6): spawns the user's terminal command and watches for the tmux client.</summary>
[SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Registered in U7 (API P10a).")]
internal sealed class ExecTemplateViewer(ExecTemplateOptions options, ISessionHost host, TimeProvider time, IRedactor? redactor = null) : ITerminalViewer
{
    public string Kind => "exec-template";

    public ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException($"{options}{host}{time}{redactor}");

    public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
}
