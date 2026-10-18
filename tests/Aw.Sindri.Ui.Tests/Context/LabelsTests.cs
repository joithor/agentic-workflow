using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context page's words live in one partial per stage (Labels.Context*.cs); two stages must not each define the same word.</summary>
[Lane(TestLane.Unit)]
public sealed class LabelsTests
{
    [Fact]
    public void No_word_is_defined_twice()
    {
        var words = typeof(Labels).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.StartsWith("Context", StringComparison.Ordinal))
            .Select(f => (f.Name, Value: (string)f.GetRawConstantValue()!))
            .ToList();

        var twice = words.GroupBy(w => w.Value, StringComparer.Ordinal).Where(g => g.Skip(1).Any()).Select(g => $"'{g.Key}' by {string.Join(", ", g.Select(w => w.Name))}").ToList();

        Assert.NotEmpty(words);
        Assert.True(twice.Count == 0, string.Join("; ", twice));
    }
}
