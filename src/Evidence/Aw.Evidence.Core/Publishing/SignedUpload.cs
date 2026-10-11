namespace Aw.Evidence.Core.Publishing;

/// <summary>A signed upload target from Linear's prepare step.</summary>
/// <param name="Url">The signed URL.</param>
/// <param name="Headers">Headers to send verbatim, in order.</param>
public sealed record SignedUpload(string Url, IReadOnlyDictionary<string, string> Headers);
