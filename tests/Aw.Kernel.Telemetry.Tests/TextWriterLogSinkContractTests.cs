using Aw.Kernel.Telemetry.Testing;
using Aw.Kernel.Testing;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class TextWriterLogSinkContractTests : LogSinkContract
{
    private readonly Dictionary<ILogSink, StringWriter> _writers = [];

    protected override ILogSink CreateSink()
    {
        var writer = new StringWriter();
        var sink = new TextWriterLogSink(writer);
        _writers[sink] = writer;
        return sink;
    }

    protected override string Delivered(ILogSink sink) => _writers[sink].ToString();
}
