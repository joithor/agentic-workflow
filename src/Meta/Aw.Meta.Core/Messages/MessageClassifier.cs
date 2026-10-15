namespace Aw.Meta.Core.Messages;

/// <summary>Classifies an inbound message body.</summary>
public static class MessageClassifier
{
    /// <summary>Classifies <paramref name="body"/>: an owner question, a ready notice, a lane announcement or a request.</summary>
    /// <param name="body">The message text.</param>
    /// <returns>The classification.</returns>
    public static Classification Classify(string body) => throw new NotSupportedException("MessageClassifier: make MessageClassifierTests pass");
}
