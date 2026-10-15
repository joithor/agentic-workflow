using System.Diagnostics;
using System.Globalization;
using Aw.Evidence.Infrastructure.Native;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class DescriptorDetourTests : IDisposable
{
    private readonly TempDirectory _root = new();

    [Fact]
    public void A_child_started_during_a_detour_writes_where_the_detour_points_and_the_descriptor_is_restored()
    {
        var captured = _root.Combine("captured.txt");
        int spare;
        using (var file = new FileStream(captured, FileMode.Create, FileAccess.Write))
        {
            spare = Libc.Duplicate(DescriptorDetour.StandardOutput);   // a descriptor we own, standing in for stdout
            Assert.True(spare > 2);
            using (DescriptorDetour.Redirect(spare, (int)file.SafeFileHandle.DangerousGetHandle()))
            {
                Echo(spare, "detoured");
            }

            Echo(spare, "restored");
            file.Flush();
        }

        _ = Libc.Close(spare);
        Assert.Equal("detoured\n", File.ReadAllText(captured));
    }

    public void Dispose() => _root.Dispose();

    // A child that writes to the given (inherited) descriptor of this process. Through /dev/fd/N, not ">&N": dash (the Linux /bin/sh) only takes a one-digit N there.
    private static void Echo(int descriptor, string text)
    {
        using var child = Process.Start("/bin/sh", ["-c", string.Create(CultureInfo.InvariantCulture, $"printf '%s\\n' '{text}' >> /dev/fd/{descriptor}")]);
        child.WaitForExit();
    }
}
