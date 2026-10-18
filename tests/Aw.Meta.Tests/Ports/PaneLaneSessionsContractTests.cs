using Aw.Kernel.Testing;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class PaneLaneSessionsContractTests : LaneSessionsContract, IDisposable
{
    private readonly TempDirectory _dir = new();
    private int _next = 100;

    protected override (ILaneSessions Sessions, Action<string, int, string> Start) Create()
    {
        var pane = new InMemoryLanePane();
        var processes = new InMemoryProcessTable();
        return (new PaneLaneSessions(pane, processes, _dir.Path), (session, depth, record) =>
        {
            var root = _next;
            _next += depth + 10;
            pane.Put(session, "claude");
            pane.SetPid(session, root);
            for (var level = 0; level <= depth; level++)
            {
                processes.Add(new ProcessInfo(root + level, level == 0 ? 1 : root + level - 1, "p" + level.ToString(System.Globalization.CultureInfo.InvariantCulture), 1, 1));
            }

            File.WriteAllText(Path.Combine(_dir.Path, (root + depth).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json"), record);
        }
        );
    }

    public void Dispose() => _dir.Dispose();
}
