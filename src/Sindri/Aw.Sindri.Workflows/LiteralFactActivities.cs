using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'StoreLiteralFacts': a child started with a literal input holds it as a fact whose ref is 'literal:&lt;compact json&gt;' and whose
/// hash is the SHA-256 of that JSON (ChildInputs). Nothing wrote the JSON into the fact store, so every reader of
/// '&lt;home&gt;/workflow/facts/&lt;sha&gt;' failed. This writes the bytes under their hash when the instance starts, so every reader keeps
/// the one path; storing the same bytes again changes nothing.
/// </summary>
/// <param name="store">Where facts are stored by their hash.</param>
internal sealed class LiteralFactActivities(IFactStore store)
{
    private const string Prefix = "literal:";

    /// <summary>Stores the JSON of each literal fact in <paramref name="facts"/>; the others are left alone.</summary>
    /// <param name="facts">The instance's start facts.</param>
    /// <returns>A task that ends when every literal is stored.</returns>
    [Activity("StoreLiteralFacts")]
    public async Task StoreAsync(IReadOnlyList<FactValue> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        foreach (var fact in facts.Where(static f => IsLiteral(f)))
        {
            var bytes = Encoding.UTF8.GetBytes(fact.Ref[Prefix.Length..]);
            if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), fact.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"the literal fact {fact.Sha256} is not the SHA-256 of its JSON; nothing is stored");
            }

            var stored = await store.Put(bytes, cancel).ConfigureAwait(false);
            if (stored.IsError)
            {
                throw new InvalidOperationException($"{stored.Error.Code}: {stored.Error.Message}");
            }
        }
    }

    /// <summary>Whether <paramref name="fact"/> is a literal: its ref carries the compact JSON itself.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>True for a 'literal:' ref.</returns>
    internal static bool IsLiteral(FactValue fact) => fact.Ref.StartsWith(Prefix, StringComparison.Ordinal);
}
