using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Unit)]
public sealed class InMemoryEndpointRecordsContractTests : EndpointRecordSourceContract
{
    protected override IEndpointRecordSource With(byte[]? record)
    {
        var records = new InMemoryEndpointRecords();
        if (record is not null)
        {
            records.WriteText(Encoding.UTF8.GetString(record));
        }

        return records;
    }
}
