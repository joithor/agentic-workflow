using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linking;

/// <summary>
/// The decision-extraction protocol (spec §6.1, §7 Egress; S-5B): the prompt for one record, and the validation of the model's answer.
/// The model only proposes: a decision is kept when its quote is found verbatim in the scrubbed record text, and it decides only nodes the
/// record's rules already link. Nothing the model says is trusted or confirmed (spec §7 trust table).
/// </summary>
public static class DecisionExtractor
{
    /// <summary>The longest quote kept.</summary>
    public const int MaxQuote = 600;

    /// <summary>The prompt for one record: the instruction, its linked node keys, and the fenced record text.</summary>
    /// <param name="record">The record.</param>
    /// <param name="text">Its cleaned text.</param>
    /// <param name="linked">The node keys its rules linked.</param>
    /// <returns>The prompt.</returns>
    public static string Prompt(RecordKey record, string text, IReadOnlyList<NodeKey> linked) => throw new NotSupportedException("S-5B slice 4");

    /// <summary>Reads the answer: JSON '{"decisions":[{"quote","wording","decides":[node keys],"replaces":quote}]}'.</summary>
    /// <param name="answer">The model's text.</param>
    /// <param name="record">The record.</param>
    /// <param name="text">The cleaned record text the quotes must appear in.</param>
    /// <param name="linked">The node keys its rules linked.</param>
    /// <param name="at">When the sync ran.</param>
    /// <returns>What was kept and how much was refused.</returns>
    public static ExtractionOutcome Read(string answer, RecordKey record, string text, IReadOnlyList<NodeKey> linked, DateTimeOffset at) => throw new NotSupportedException("S-5B slice 4");
}
