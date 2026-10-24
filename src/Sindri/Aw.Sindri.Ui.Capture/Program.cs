using Avalonia.Headless;

namespace Aw.Sindri.Ui.Capture;

/// <summary>
/// The entry point: 'sindri-ui-capture --size WxH --shot &lt;route&gt; &lt;png&gt; ...'. Exit 0 when every frame was written, 1 for a usage mistake, 4 when
/// the capture could not be made (the control plane is not running, a frame was not rendered); the reason is one line on stderr.
/// </summary>
internal static class Program
{
    private const string Prefix = "sindri-ui-capture: ";

    private static async Task<int> Main(string[] args)
    {
        if (CaptureArguments.Parse(args) is not { } arguments)
        {
            await Console.Error.WriteLineAsync(Prefix + "usage: " + CaptureArguments.Usage).ConfigureAwait(false);
            return 1;
        }

        using var session = HeadlessUnitTestSession.StartNew(typeof(CaptureApp));
        var home = CaptureRun.HomeOf(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        try
        {
            var problem = await session.Dispatch(() => new CaptureRun(home).RunAsync(arguments, CancellationToken.None), CancellationToken.None).ConfigureAwait(false);
            if (problem is null)
            {
                return 0;
            }

            await Console.Error.WriteLineAsync(Prefix + problem.ReplaceLineEndings(" ")).ConfigureAwait(false);
            return 4;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync(Prefix + ex.Message.ReplaceLineEndings(" ")).ConfigureAwait(false);
            return 4;
        }
    }
}
