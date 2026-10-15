namespace Aw.Meta.Core.Messages;

/// <summary>The result of classifying a message body.</summary>
/// <param name="Kind">What the message is.</param>
/// <param name="Pr">The pull request number of a ready message.</param>
/// <param name="Lane">The lane name of an announcement, lower case.</param>
public sealed record Classification(MessageKind Kind, int? Pr = null, string? Lane = null);
