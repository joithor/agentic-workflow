using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// The docker adapter over a scripted runner whose database holds one default Admin group (or whose project has no database container).
[Lane(TestLane.Unit)]
public sealed class DockerExecOnyxDatabaseContractTests : OnyxDatabaseContract
{
    protected override IOnyxDatabase Create(int? group)
        => new DockerExecOnyxDatabase(new FakeToolRunner((_, arguments) => arguments[0] switch
        {
            "ps" => new ToolRun(ToolOutcome.Exited, 0, group is null ? string.Empty : "0123456789ab\n"),
            _ => new ToolRun(ToolOutcome.Exited, 0, (group?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty) + "\n"),
        }));
}
