#pragma warning disable
namespace Aw.Sindri.Ui.Views;

internal static class Dbg
{
    private static int _n;
    internal static void M(string s) { if (_n < 300) System.IO.File.AppendAllText("/home/thor/repos/aw-wt/sindri-ui-needs-you-3/ny3-marks.txt", "  " + s + "\n"); }
    internal static void Deep(string s)
    {
        if (System.Threading.Interlocked.Increment(ref _n) == 2000)
        {
            System.IO.File.AppendAllText("/home/thor/repos/aw-wt/sindri-ui-needs-you-3/ny3-marks.txt", "STACK " + s + "\n" + System.Environment.StackTrace + "\n");
        }
    }
}
