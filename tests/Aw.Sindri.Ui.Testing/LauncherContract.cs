using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract every <see cref="ILauncher"/> passes: the fake, and the Avalonia adapter bound over a headless window. A link goes through,
/// a file opens only when it is an absolute path to a file that is there, and a launcher the platform refuses answers false instead of throwing.
/// </summary>
public abstract class LauncherContract
{
    /// <summary>A launcher the platform accepts requests on.</summary>
    /// <returns>The launcher.</returns>
    protected abstract ILauncher Create();

    /// <summary>A launcher the platform refuses every request on (no window yet, say).</summary>
    /// <returns>The launcher.</returns>
    protected abstract ILauncher CreateRefusing();

    /// <summary>The absolute path of a file that is there, as <paramref name="launcher"/>'s platform sees it.</summary>
    /// <param name="launcher">A launcher from <see cref="Create"/>.</param>
    /// <returns>The path.</returns>
    protected abstract string ExistingFile(ILauncher launcher);

    /// <summary>False when the platform under the adapter opens nothing (Avalonia's headless launcher), so only the refusals can be checked.</summary>
    protected virtual bool CanLaunch => true;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_http_link_goes_through()
    {
        if (CanLaunch)
        {
            Expect.True(await Create().OpenUri(new Uri("https://example.test/page"), Token));
        }
    }

    [Fact]
    public async Task An_existing_absolute_file_opens()
    {
        var launcher = Create();

        if (CanLaunch)
        {
            Expect.True(await launcher.OpenFile(ExistingFile(launcher), Token));
        }
    }

    [Fact]
    public async Task A_relative_path_is_false()
    {
        var launcher = Create();

        Expect.True(!await launcher.OpenFile(Path.Combine("docs", "prd.md"), Token));
    }

    [Fact]
    public async Task A_missing_file_is_false()
    {
        var launcher = Create();

        Expect.True(!await launcher.OpenFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.md"), Token));
    }

    [Fact]
    public async Task A_refusing_launcher_is_false_for_a_link_and_a_file()
    {
        var launcher = CreateRefusing();

        Expect.True(!await launcher.OpenUri(new Uri("https://example.test/page"), Token));
        Expect.True(!await launcher.OpenFile(ExistingFile(Create()), Token));
    }
}
