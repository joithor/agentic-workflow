using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// 'fact:redTests' as one reader sees it (line spec A.7, §7.2): an object keyed by item id, each value a list of test ids (<c language="text">["T1"]</c>) or
/// <c language="text">{tests?: [id | {id, path}], appOnly?: {signal}, commit?, output?}</c>. Red-check, before/after and PRV-RED-001 all parse it here.
/// </summary>
/// <param name="Entries">One entry per key, in the fact's order.</param>
/// <param name="Problem">Why the fact as a whole is not an object; null otherwise.</param>
public sealed record RedTestsFact(IReadOnlyList<RedEntry> Entries, string? Problem)
{
    /// <summary>The shape, as the agent and every refusal state it.</summary>
    public const string Shape = "";

    /// <summary>Parses <paramref name="fact"/>; null and <c language="text">{}</c> hold no entry.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>The parsed fact.</returns>
    public static RedTestsFact Parse(JsonNode? fact) => throw new NotSupportedException();

    /// <summary>The red commit the entries name and why they can't be used as one, if they can't: none named, or two different ones.</summary>
    /// <returns>The commit, or the problem.</returns>
    public (string? Commit, string? Problem) RedCommit() => throw new NotSupportedException();
}

/// <summary>One item's entry of <see cref="RedTestsFact"/>.</summary>
/// <param name="Item">The key.</param>
/// <param name="Tests">The tests it names.</param>
/// <param name="AppOnly">Whether it records an app-only signal instead of (or beside) tests.</param>
/// <param name="Commit">The entry's red commit; null when it names none.</param>
/// <param name="Problem">Why the entry is in none of the accepted shapes, naming the item; null otherwise.</param>
public sealed record RedEntry(string Item, IReadOnlyList<RedTest> Tests, bool AppOnly, string? Commit, string? Problem);

/// <summary>One red test.</summary>
/// <param name="Id">The test id.</param>
/// <param name="Path">The file it is in; null for the short form.</param>
public sealed record RedTest(string Id, string? Path);
