using Aw.Meta.Core.Health;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Parses the text of the macOS tools the macOS host adapters run: sysctl, vm_stat and ps.</summary>
internal static class MacFormat
{
    /// <summary>The total memory in kilobytes, from "sysctl -n hw.memsize" (bytes).</summary>
    /// <param name="sysctl">The tool's output.</param>
    /// <returns>Kilobytes.</returns>
    internal static long MemTotalKb(string sysctl) => throw new NotSupportedException();

    /// <summary>The available memory in kilobytes, from vm_stat: free, inactive and speculative pages times the page size.</summary>
    /// <param name="vmStat">The tool's output.</param>
    /// <returns>Kilobytes.</returns>
    internal static long AvailableKb(string vmStat) => throw new NotSupportedException();

    /// <summary>The swap total and used, in kilobytes, from "sysctl -n vm.swapusage".</summary>
    /// <param name="swapUsage">The tool's output.</param>
    /// <returns>Total and used kilobytes.</returns>
    internal static (long TotalKb, long UsedKb) SwapUsage(string swapUsage) => throw new NotSupportedException();

    /// <summary>The processes of "ps -axww -o pid=,ppid=,etime=,rss=,stat=,args=". Zombies are left out.</summary>
    /// <param name="ps">The tool's output.</param>
    /// <returns>The live processes.</returns>
    internal static IReadOnlyList<ProcessInfo> Processes(string ps) => throw new NotSupportedException();
}
