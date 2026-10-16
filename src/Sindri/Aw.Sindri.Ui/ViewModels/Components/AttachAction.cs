namespace Aw.Sindri.Ui.ViewModels.Components;

/// <summary>An action the Attach component can ask for.</summary>
internal enum AttachAction
{
    /// <summary>Join the live session.</summary>
    Attach,

    /// <summary>Open the instance's frame.</summary>
    OpenInstance,

    /// <summary>Take the Step over.</summary>
    TakeOver,

    /// <summary>File a request against the Step.</summary>
    FileRequest,

    /// <summary>Cancel the Step (asked first).</summary>
    Cancel,
}
