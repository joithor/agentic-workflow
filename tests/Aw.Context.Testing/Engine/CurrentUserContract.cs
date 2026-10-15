using Aw.Context.Core.Engine;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>The current user port's contract (S-4): a name that is not blank and fits on one line.</summary>
public abstract class CurrentUserContract
{
    /// <summary>The name is something to record.</summary>
    [Fact]
    public void The_name_is_not_blank_and_is_one_line()
    {
        const string test = nameof(The_name_is_not_blank_and_is_one_line);
        var name = CreateUser().Name;

        Expect.That(!string.IsNullOrWhiteSpace(name) && !name.Any(char.IsControl), test, $"the name was '{name}'");
    }

    /// <summary>A user to ask.</summary>
    /// <returns>The user.</returns>
    protected abstract ICurrentUser CreateUser();
}
