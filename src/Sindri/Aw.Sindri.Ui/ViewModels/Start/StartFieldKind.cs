namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>How an input is asked for, by the fact type it declares.</summary>
internal enum StartFieldKind
{
    /// <summary>A text box (type text).</summary>
    Text,

    /// <summary>A file chosen at the desk (type file).</summary>
    File,

    /// <summary>A link box (type link).</summary>
    Link,

    /// <summary>JSON text (type payload, and any type this build has no field for).</summary>
    Payload,
}
