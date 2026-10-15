using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Engine;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The local activities that read a stored fact for the interpreter, so workflow code does no I/O. Each runs once, on start:
/// 'ReadItemRepo' (engine spec rev 8 §5.1 'Repo', §12 'fact:item') returns the repo a 'fact:item' names, only when the input
/// has one; 'ReadSubject' returns the first input's content as text, for the 'Subject' attribute and the view's subject.
/// </summary>
/// <param name="home">Where the facts are.</param>
internal sealed class FactReadActivities(WorkflowsHome home)
{
    private const int SubjectBytes = 64 * 1024;

    private static readonly System.Text.Json.JsonSerializerOptions _options = AwJson.CreateOptions(WorkflowsJsonContext.Default);

    /// <summary>The repo 'fact:item' names.</summary>
    /// <param name="item">The stored 'fact:item'.</param>
    /// <returns>Its repo; null when it names none or isn't an item (the attribute stays unset).</returns>
    [Activity("ReadItemRepo")]
    public async Task<string?> ReadRepoAsync(FactValue item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var file = Path.Combine(home.FactsDirectory, Path.GetFileName(item.Sha256));
        var content = await File.ReadAllBytesAsync(file, ActivityExecutionContext.Current.CancellationToken).ConfigureAwait(false);
        var read = AwJson.Deserialize<ItemFact>(content, _options);
        return read.IsError || read.Value.Repo is not { Length: > 0 } repo ? null : repo;
    }

    /// <summary>
    /// 'ReadSubject': the first input's stored content, at most 64 KiB as text, so the interpreter names the subject (a text's
    /// first line, a link's tracker key, a payload's id) without I/O in workflow code.
    /// </summary>
    /// <param name="fact">The stored first input.</param>
    /// <returns>Its text; null when it is not stored.</returns>
    [Activity("ReadSubject")]
    public async Task<string?> ReadSubjectAsync(FactValue fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var file = Path.Combine(home.FactsDirectory, Path.GetFileName(fact.Sha256));
        if (!File.Exists(file))
        {
            return null;   // boundary: content that is not stored leaves the subject named by its fact key
        }

        var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[SubjectBytes];
            var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ActivityExecutionContext.Current.CancellationToken).ConfigureAwait(false);
            var chars = new char[System.Text.Encoding.UTF8.GetMaxCharCount(read)];
            var count = System.Text.Encoding.UTF8.GetDecoder().GetChars(buffer, 0, read, chars, 0, flush: false);   // why: flush: false leaves out a character the 64 KiB cut split, so no U+FFFD ends the text
            return new string(chars, 0, count);
        }
    }
}
