namespace Aw.Sindri.Host.Commands;

/// <summary>What 'context map review' does.</summary>
internal enum ReviewMode
{
    /// <summary>The loop at a terminal: the bulk accept, then each item.</summary>
    Interactive,

    /// <summary>List the queue (read-only).</summary>
    Queue,

    /// <summary>List the override log.</summary>
    History,

    /// <summary>Write the queue as a review file.</summary>
    Export,

    /// <summary>Send one review request.</summary>
    Request,
}
