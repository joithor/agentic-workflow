using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

/// <summary>The stream picker the Avalonia adapter reads through: the contract, and what only a platform can do (an unknown size, a refusal, a stream that is read no further than needed).</summary>
[Lane(TestLane.Unit)]
public sealed class StreamFilePickerTests : FilePickerContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    protected override IFilePicker Chooses(string name, byte[] content) =>
        new StreamFilePicker((_, _) => Task.FromResult<ChosenFile?>(new ChosenFile(name, content.Length, _ => Task.FromResult<Stream>(new MemoryStream(content)))));

    protected override IFilePicker ChoosesNothing() => new StreamFilePicker((_, _) => Task.FromResult<ChosenFile?>(null));

    [Fact]
    public async Task A_file_of_unknown_size_over_the_limit_is_measured_by_reading_one_byte_past_it()
    {
        var stream = new CountingStream(new byte[1000]);
        var picker = new StreamFilePicker((_, _) => Task.FromResult<ChosenFile?>(new ChosenFile("odd.bin", Size: null, _ => Task.FromResult<Stream>(stream))));

        var picked = await picker.Pick("Choose", 10, Token);

        Expect.True(picked is { Size: 11 } && picked.Content.IsEmpty && stream.Total == 11);
    }

    [Fact]
    public async Task A_platform_that_refuses_is_no_choice()
    {
        var picker = new StreamFilePicker((_, _) => throw new UnauthorizedAccessException("sandbox"));

        Expect.True(await picker.Pick("Choose", 10, Token) is null);
    }

    [Fact]
    public async Task The_title_reaches_the_chooser()
    {
        string? seen = null;
        var picker = new StreamFilePicker((title, _) =>
        {
            seen = title;
            return Task.FromResult<ChosenFile?>(null);
        });

        _ = await picker.Pick("Pick the spec", 10, Token);

        Expect.True(seen == "Pick the spec");
    }

    private sealed class CountingStream(byte[] content) : MemoryStream(content)
    {
        public int Total { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = Read(buffer.Span);
            Total += read;
            return ValueTask.FromResult(read);
        }
    }
}
