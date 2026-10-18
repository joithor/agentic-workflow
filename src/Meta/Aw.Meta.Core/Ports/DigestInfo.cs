namespace Aw.Meta.Core.Ports;

/// <summary>A digest file a lane wrote.</summary>
/// <param name="Length">Its size in bytes.</param>
/// <param name="ModifiedAt">When it was last written.</param>
public sealed record DigestInfo(long Length, DateTimeOffset ModifiedAt);
