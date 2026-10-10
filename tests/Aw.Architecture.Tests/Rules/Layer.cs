namespace Aw.Architecture.Tests.Rules;

/// <summary>Where a type sits in a package's shape (spec §4b.1), from its namespace.</summary>
public enum Layer
{
    /// <summary>Aw.&lt;Pkg&gt;.Core.</summary>
    Core,

    /// <summary>Aw.&lt;Pkg&gt;.Features, or App.Features in the small shape.</summary>
    Features,

    /// <summary>Aw.&lt;Pkg&gt;.Infrastructure, or App.Infrastructure in the small shape.</summary>
    Infrastructure,

    /// <summary>App.Commands in the small shape (full-shape Host.Commands and Host.Endpoints are Host).</summary>
    Commands,

    /// <summary>Aw.&lt;Pkg&gt;.Host (with its Commands and Endpoints), or the small shape's App root namespace (Composition, Program).</summary>
    Host,
}
