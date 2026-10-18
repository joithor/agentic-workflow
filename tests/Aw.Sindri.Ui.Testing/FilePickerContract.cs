using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract every <see cref="IFilePicker"/> passes: the fake, and the stream picker the Avalonia adapter reads through. A chosen file comes back
/// with its name, size and bytes; one larger than the limit comes back with its size and no bytes, and the limit itself still reads; no choice is null.
/// </summary>
public abstract class FilePickerContract
{
    private const int Limit = 16;

    /// <summary>A picker whose human chooses a file with this name and content.</summary>
    /// <param name="name">The file's name.</param>
    /// <param name="content">Its bytes.</param>
    /// <returns>The picker.</returns>
    protected abstract IFilePicker Chooses(string name, byte[] content);

    /// <summary>A picker whose human chooses nothing.</summary>
    /// <returns>The picker.</returns>
    protected abstract IFilePicker ChoosesNothing();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_file_within_the_limit_comes_back_whole()
    {
        var picked = await Chooses("spec.md", [1, 2, 3]).Pick("Choose", Limit, Token);

        Expect.True(picked is { Size: 3 } && string.Equals(picked.Name, "spec.md", StringComparison.Ordinal) && picked.Content.Span.SequenceEqual((byte[])[1, 2, 3]));
    }

    [Fact]
    public async Task A_file_of_exactly_the_limit_still_reads()
    {
        var picked = await Chooses("edge.bin", new byte[Limit]).Pick("Choose", Limit, Token);

        Expect.True(picked is { Size: Limit } && picked.Content.Length == Limit);
    }

    [Fact]
    public async Task A_file_one_byte_over_the_limit_is_reported_by_its_size_and_left_unread()
    {
        var picked = await Chooses("big.bin", new byte[Limit + 1]).Pick("Choose", Limit, Token);

        Expect.True(picked is { Size: Limit + 1 } && string.Equals(picked.Name, "big.bin", StringComparison.Ordinal) && picked.Content.IsEmpty);
    }

    [Fact]
    public async Task A_file_far_over_the_limit_is_left_unread()
    {
        var picked = await Chooses("huge.bin", new byte[Limit * 100]).Pick("Choose", Limit, Token);

        Expect.True(picked is { Size: Limit * 100 } && picked.Content.IsEmpty);
    }

    [Fact]
    public async Task No_choice_is_null() => Expect.True(await ChoosesNothing().Pick("Choose", Limit, Token) is null);
}
