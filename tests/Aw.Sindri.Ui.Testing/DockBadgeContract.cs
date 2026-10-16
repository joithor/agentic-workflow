using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract every <see cref="IDockBadge"/> passes: the fake, the null badge and, on macOS only, the dock tile.</summary>
public abstract class DockBadgeContract
{
    /// <summary>A fresh badge.</summary>
    /// <returns>The badge.</returns>
    protected abstract IDockBadge Create();

    [Fact]
    public void It_starts_clear() => Expect.Equal(0, Create().Shown);

    [Fact]
    public void Shows_a_count()
    {
        var badge = Create();

        badge.Show(3);

        Expect.Equal(3, badge.Shown);
    }

    [Fact]
    public void Zero_clears_the_badge()
    {
        var badge = Create();
        badge.Show(3);

        badge.Show(0);

        Expect.Equal(0, badge.Shown);
    }

    [Fact]
    public void Last_write_wins()
    {
        var badge = Create();

        badge.Show(2);
        badge.Show(7);

        Expect.Equal(7, badge.Shown);
    }
}
