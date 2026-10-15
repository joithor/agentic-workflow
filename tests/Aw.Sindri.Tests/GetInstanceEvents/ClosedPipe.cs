using System.Text;

namespace Aw.Sindri.Tests.GetInstanceEvents;

/// <summary>A stdout whose reader has gone, as in 'sindri workflow events T-1 --follow | head -n 3': every write fails with an IOException.</summary>
internal sealed class ClosedPipe : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override Task WriteAsync(string? value) => throw new IOException("Broken pipe");

    public override Task WriteLineAsync(string? value) => throw new IOException("Broken pipe");
}
