namespace Aw.Redaction;

/// <summary>A hit before merging: the rule, its span and the rule's priority (lower wins at the same start).</summary>
internal readonly record struct RawHit(string Rule, int Start, int End, int Order);
