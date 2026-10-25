namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One input fact of a start (POST /workflow/instances): a name and its content, which travels as base64 and is at most 1 MiB.</summary>
/// <param name="Name">The fact's file name, shown with it in the frame.</param>
/// <param name="Content">The bytes: a text as UTF-8, a file as it is.</param>
internal sealed record StartFact(string Name, ReadOnlyMemory<byte> Content);
