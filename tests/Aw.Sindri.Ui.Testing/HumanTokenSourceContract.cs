using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract every <see cref="IHumanTokenSource"/> passes: the in-memory one and each head's adapter.</summary>
public abstract class HumanTokenSourceContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A source over the given token file contents, or none.</summary>
    /// <param name="contents">The text serve wrote (with its newline), or null for no token.</param>
    /// <returns>The source.</returns>
    protected abstract IHumanTokenSource With(string? contents);

    /// <summary>Replaces what <paramref name="source"/> reads, as a restarted serve's new token does; null removes it.</summary>
    /// <param name="source">A source made by <see cref="With"/>.</param>
    /// <param name="contents">The new contents, or null.</param>
    protected abstract void Rewrite(IHumanTokenSource source, string? contents);

    [Fact]
    public async Task No_token_reads_as_null() => Expect.True(await With(null).Read(Token) is null);

    [Fact]
    public async Task A_token_reads_back_without_its_newline() =>
        Expect.Equal("pT3x-9_token", await With("pT3x-9_token\n").Read(Token));

    [Fact]
    public async Task An_empty_token_reads_as_null() => Expect.True(await With("\n").Read(Token) is null);

    // The app reads the token at each call (like the CLI), so a restarted control plane's token is picked up with no app restart.
    [Fact]
    public async Task Each_read_sees_the_token_as_it_is_now()
    {
        var source = With("first");
        Expect.Equal("first", await source.Read(Token));

        Rewrite(source, "second\n");
        Expect.Equal("second", await source.Read(Token));

        Rewrite(source, contents: null);
        Expect.True(await source.Read(Token) is null);
    }

    [Fact]
    public void It_names_where_it_reads() => Expect.True(!string.IsNullOrWhiteSpace(With(null).Location));
}
