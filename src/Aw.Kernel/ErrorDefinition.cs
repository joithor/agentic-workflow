namespace Aw.Kernel;

/// <summary>One entry in an error catalogue: a stable code, its kind and a short title.</summary>
/// <param name="Code">The code, 'AW-AREA-NNN' or 'SND-AREA-NNN'.</param>
/// <param name="Kind">The kind every error with this code has.</param>
/// <param name="Title">A short description of the code, for the catalogue docs.</param>
public sealed record ErrorDefinition(string Code, ErrorKind Kind, string Title)
{
    /// <summary>Makes an <see cref="Error"/> with this code and kind.</summary>
    /// <param name="message">What happened in this instance.</param>
    /// <returns>The error.</returns>
    public Error With(string message) => new(Code, Kind, message);
}
