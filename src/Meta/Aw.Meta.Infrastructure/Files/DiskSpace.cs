namespace Aw.Meta.Infrastructure.Files;

/// <summary>Free space on the disk holding a directory.</summary>
internal static class DiskSpace
{
    internal static long Free(string directory)
    {
        try
        {
            return new DriveInfo(Directory.Exists(directory) ? directory : "/").AvailableFreeSpace;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
