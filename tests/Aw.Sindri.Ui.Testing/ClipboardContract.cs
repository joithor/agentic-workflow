using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract every <see cref="IClipboard"/> passes: the fake, and the Avalonia adapter bound over a headless window.
/// A write that the platform takes holds the text and answers true; one it refuses answers false and changes nothing.
/// </summary>
public abstract class ClipboardContract
{
    /// <summary>A clipboard the platform accepts writes on.</summary>
    /// <returns>The clipboard.</returns>
    protected abstract IClipboard Create();

    /// <summary>A clipboard the platform refuses writes on (no window yet, say).</summary>
    /// <returns>The clipboard.</returns>
    protected abstract IClipboard CreateRefusing();

    /// <summary>What <paramref name="clipboard"/>'s platform holds now, or null when nothing.</summary>
    /// <param name="clipboard">A clipboard from <see cref="Create"/>.</param>
    /// <returns>The text.</returns>
    protected abstract Task<string?> Held(IClipboard clipboard);

    /// <summary>False when the platform under the adapter can't be read back (Avalonia's headless clipboard keeps nothing).</summary>
    protected virtual bool CanReadBack => true;

    [Fact]
    public async Task A_written_text_is_held_and_acknowledged()
    {
        var clipboard = Create();

        var written = await clipboard.SetText("sindri views rebuild", TestContext.Current.CancellationToken);

        Expect.True(written);
        if (CanReadBack)
        {
            Expect.Equal("sindri views rebuild", await Held(clipboard));
        }
    }

    [Fact]
    public async Task A_later_write_replaces_the_text()
    {
        var clipboard = Create();

        _ = await clipboard.SetText("first", TestContext.Current.CancellationToken);
        var second = await clipboard.SetText("second", TestContext.Current.CancellationToken);

        Expect.True(second);
        if (CanReadBack)
        {
            Expect.Equal("second", await Held(clipboard));
        }
    }

    [Fact]
    public async Task A_refused_write_is_false_and_holds_nothing()
    {
        var clipboard = CreateRefusing();

        var written = await clipboard.SetText("sindri doctor", TestContext.Current.CancellationToken);

        Expect.True(!written);
        Expect.True(await Held(clipboard) is null);
    }
}
