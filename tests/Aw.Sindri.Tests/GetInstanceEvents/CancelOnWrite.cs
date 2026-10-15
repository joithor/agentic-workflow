namespace Aw.Sindri.Tests.GetInstanceEvents;

/// <summary>A stdout that interrupts the command as soon as it has been written to, like Ctrl-C right after the first events.</summary>
/// <param name="interrupt">What to cancel.</param>
internal sealed class CancelOnWrite(CancellationTokenSource interrupt) : StringWriter
{
    public override async Task WriteAsync(string? value)
    {
        await base.WriteAsync(value);
        await interrupt.CancelAsync();
    }
}
