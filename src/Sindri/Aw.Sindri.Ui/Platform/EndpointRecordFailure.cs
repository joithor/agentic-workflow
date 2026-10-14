namespace Aw.Sindri.Ui.Platform;

/// <summary>Why the platform couldn't read an endpoint record that is there (<see cref="EndpointRecordRead.Failure"/>).</summary>
public enum EndpointRecordFailure
{
    /// <summary>Reading it failed in a way the platform doesn't name further (an I/O error).</summary>
    Unreadable,

    /// <summary>This user has no permission to read it.</summary>
    NoPermission,

    /// <summary>It isn't a file: something else, such as a directory, is in its place.</summary>
    NotAFile,
}
