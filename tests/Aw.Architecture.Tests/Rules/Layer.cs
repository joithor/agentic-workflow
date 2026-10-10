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

    /// <summary>Aw.&lt;Pkg&gt;.Host.Commands or Host.Endpoints, or App.Commands in the small shape.</summary>
    Commands,

    /// <summary>The rest of Aw.&lt;Pkg&gt;.Host, or the App root namespace (Composition) in the small shape.</summary>
    Host,
}
