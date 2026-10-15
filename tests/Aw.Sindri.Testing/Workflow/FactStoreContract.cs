using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The contract every <see cref="IFactStore"/> honours (engine spec rev 8.1 §5.3, §6.1), run against the fake and the home's files.</summary>
public abstract class FactStoreContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>An empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IFactStore Create();

    /// <summary>A stored fact is named by its content's SHA-256, as 'fact:sha256:&lt;hex&gt;', with no git head.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Put_names_the_content_by_its_sha256()
    {
        var content = Encoding.UTF8.GetBytes("the output");
        var sha = Convert.ToHexStringLower(SHA256.HashData(content));

        var stored = await Create().Put(content, Token);

        Expect.True(stored.IsSuccess);
        Expect.Equal(new Core.Workflow.Engine.FactValue("fact:sha256:" + sha, sha, HeadSha: null), stored.Value);
    }

    /// <summary>Storing the same content again returns the same fact; different content gets its own.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_same_content_is_the_same_fact()
    {
        var store = Create();

        var first = await store.Put(Encoding.UTF8.GetBytes("one"), Token);
        var again = await store.Put(Encoding.UTF8.GetBytes("one"), Token);
        var other = await store.Put(Encoding.UTF8.GetBytes("two"), Token);

        Expect.Equal(first.Value, again.Value);
        Expect.True(first.Value != other.Value);
    }
}
