using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Tests.Adapters;

public sealed class ProcessScannerLsofTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task Where_there_is_no_proc_the_scan_runs_the_lsof_it_was_given_and_names_a_missing_one_as_AW_META_010()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "the lsof fallback only runs where there is no /proc");

        var locations = await new ProcessScanner("/nonexistent/lsof").Locations(TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, locations.Error.Code);
        Assert.Contains("the process scan failed", locations.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_lsof_macOS_ships_lists_this_process()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "the lsof fallback only runs where there is no /proc");

        var locations = await new ProcessScanner(MacToolPaths.Resolve(File.Exists).Lsof).Locations(TestContext.Current.CancellationToken);

        Assert.Contains(locations.Value, location => location.Pid == Environment.ProcessId);
    }
}
