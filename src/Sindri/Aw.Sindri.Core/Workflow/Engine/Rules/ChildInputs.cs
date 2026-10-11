using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>The id and the input facts of a child a 'waitFor: { child }' creates (spec §5.7).</summary>
internal static class ChildInputs
{
    /// <summary>The child's id: a UUIDv8 over SHA-256 of '{parent trigger key}:child', so a replayed Commit creates the same child.</summary>
    /// <param name="key">The parent's trigger key.</param>
    /// <returns>The child's id.</returns>
    public static WorkflowInstanceId IdFor(TriggerKey key)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(Encoding.UTF8.GetBytes(key.Value + ":child"), digest);
        var bytes = digest[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return WorkflowInstanceId.For(new Guid(bytes, bigEndian: true));
    }

    /// <summary>
    /// The child's inputs: a '{ fact }' input copies the parent's fact, a literal is recorded by its JSON, and a
    /// '{ profile }' input is left to the child, which reads the profile itself.
    /// </summary>
    /// <param name="parent">The parent.</param>
    /// <param name="wait">The child wait.</param>
    /// <returns>The input facts.</returns>
    public static IReadOnlyDictionary<FactKey, FactValue> From(WorkflowInstance parent, ChildWait wait)
    {
        var inputs = new Dictionary<FactKey, FactValue>();
        foreach (var (key, value) in wait.Inputs)
        {
            if (value.Fact is { } fact && parent.Facts.TryGetValue(fact, out var held))
            {
                inputs[key] = held;
            }
            else if (value.Literal is { } literal)
            {
                inputs[key] = Literal(literal);
            }
        }

        return inputs;
    }

    // A literal has no blob, so its ref carries the compact JSON itself: 'literal:{json}', with the SHA-256 of that JSON.
    // This ref convention is accepted for now (P3 coordinator, unit (d)); it isn't in the spec yet.
    private static FactValue Literal(JsonElement literal)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            literal.WriteTo(writer);
        }

        return new FactValue("literal:" + Encoding.UTF8.GetString(buffer.WrittenSpan), Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan)), HeadSha: null);
    }
}
