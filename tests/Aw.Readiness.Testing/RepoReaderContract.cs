using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Testing;

/// <summary>The contract every <see cref="IRepoReader"/> passes (the in-memory fake and the file-system adapter): an
/// existing checkout opens, anything else is AW-READINESS-002 with a fix.</summary>
public abstract class RepoReaderContract
{
    /// <summary>A reader where <see cref="ExistingRoot"/> is a checkout holding README.md, <see cref="FileRoot"/> is a
    /// file and <see cref="MissingRoot"/> doesn't exist.</summary>
    /// <returns>The reader.</returns>
    protected abstract IRepoReader NewReader();

    /// <summary>A checkout root that exists.</summary>
    protected abstract string ExistingRoot { get; }

    /// <summary>A path that is a file, not a directory.</summary>
    protected abstract string FileRoot { get; }

    /// <summary>A path that doesn't exist.</summary>
    protected abstract string MissingRoot { get; }

    /// <summary>An existing root opens and shows its files.</summary>
    [Fact]
    public void An_existing_root_opens()
    {
        var tree = NewReader().Open(ExistingRoot);

        Expect.True(tree.IsSuccess, "Open failed for an existing checkout");
        Expect.True(tree.Value.Exists("README.md"), "the opened tree doesn't show README.md");
    }

    /// <summary>A missing root and a file root are AW-READINESS-002, with a fix.</summary>
    [Fact]
    public void A_missing_or_file_root_is_not_found()
    {
        var reader = NewReader();

        foreach (var root in new[] { MissingRoot, FileRoot })
        {
            var tree = reader.Open(root);
            Expect.True(tree.IsError, $"Open({root}) succeeded");
            Expect.Equal("AW-READINESS-002", tree.Error.Code, $"Open({root}) error code");
            Expect.True(tree.Error.Message.Contains("fix:", StringComparison.Ordinal), $"Open({root}) names no fix");
        }
    }
}
