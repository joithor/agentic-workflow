// The structural guard for the silent {} failure (R9): every Vogen value object must carry a [JsonConverter]
// whose converter is a top-level user-code type, and must have no Vogen-generated STJ converter nested in it.
// Runs in the unit lane (JIT); in the product it belongs in Aw.Architecture.Tests over every assembly.
using System.Reflection;
using System.Text.Json.Serialization;
using Xunit;

namespace Spike.R9.Guard;

public static class IdGuard
{
    public static IReadOnlyList<string> Violations(Assembly assembly)
    {
        var problems = new List<string>();
        foreach (var t in assembly.GetTypes())
        {
            var isValueObject = t.GetCustomAttributes(inherit: false)
                .Any(a => a.GetType().Name.StartsWith("ValueObjectAttribute", StringComparison.Ordinal));
            if (!isValueObject) continue;
            var conv = t.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType;
            if (conv is null) problems.Add($"{t.Name}: no [JsonConverter]");
            else if (conv.DeclaringType is not null) problems.Add($"{t.Name}: converter {conv.Name} is generated (nested), not user code");
            var generated = t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(n => n.Name.EndsWith("SystemTextJsonConverter", StringComparison.Ordinal)).Select(n => n.Name).ToList();
            if (generated.Count > 0) problems.Add($"{t.Name}: Vogen STJ conversions are on ({string.Join(", ", generated)})");
        }
        return problems;
    }
}

[Trait("Lane", "unit")]
public sealed class IdGuardTests(ITestOutputHelper output)
{
    [Fact]
    public void Hybrid_ids_pass_the_guard()
    {
        var v = IdGuard.Violations(Assembly.Load("VogenHybrid"));
        foreach (var x in v) output.WriteLine(x);
        Assert.Empty(v);
    }

    [Fact]
    public void Guard_catches_ids_with_Vogen_STJ_conversions()
    {
        var v = IdGuard.Violations(Assembly.Load("VogenIds"));
        foreach (var x in v) output.WriteLine(x);
        Assert.Contains(v, x => x.StartsWith("ItemId:", StringComparison.Ordinal));
        Assert.Contains(v, x => x.StartsWith("TokenBudget:", StringComparison.Ordinal));
    }
}
