namespace Aw.Evidence.Core.Data;

/// <summary>An AW-EVIDENCE code and its message, as a summary records a refusal or an error.</summary>
/// <param name="Code">The catalogue code.</param>
/// <param name="Message">The message of the stderr line.</param>
public sealed record CodedMessage(string Code, string Message);
