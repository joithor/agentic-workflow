using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract every <see cref="IHumanTokenSource"/> passes: the in-memory one and each head's adapter.</summary>
public abstract class HumanTokenSourceContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A source holding the given text as 'sindri serve' wrote it, or none.</summary>
    /// <param name="text">The file's text (the token and a newline), or null for no token.</param>
    /// <returns>The source.</returns>
    protected abstract IHumanTokenSource With(string? text);

    [Fact]
    public async Task No_token_reads_as_null() => Expect.True(await With(null).Read(Token) is null);

    [Fact]
    public async Task A_token_reads_back_without_its_newline() => Expect.Equal("q3Zx-Tok_9", await With("q3Zx-Tok_9\n").Read(Token));

    [Fact]
    public async Task A_blank_token_reads_as_null() => Expect.True(await With("  \n").Read(Token) is null);

    [Fact]
    public void It_names_where_it_reads() => Expect.True(!string.IsNullOrWhiteSpace(With(null).Location));
}
