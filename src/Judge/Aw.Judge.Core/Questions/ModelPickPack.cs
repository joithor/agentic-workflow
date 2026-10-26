using Aw.Judge.Core.Ports;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// The filters a model-pick input passes before any provider sees it: denyPaths, the built-in secret file names and a
/// secret scan, applied to the files, the facts and the brief, then the size cap.
/// </summary>
public static class ModelPickPack
{
    /// <summary>Filters and caps an input.</summary>
    /// <param name="input">The call's validated input.</param>
    /// <param name="redactor">The judge's redactor, which is also the secret scan.</param>
    /// <returns>The input to send: kept files, the brief masked, and the omitted, cut and withheld records set.</returns>
    public static ModelPickInput Apply(ModelPickInput input, IInputRedactor redactor) => throw new NotSupportedException("model-pick is not built yet");
}
