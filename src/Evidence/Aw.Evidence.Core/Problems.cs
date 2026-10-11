using Aw.Kernel;

namespace Aw.Evidence.Core;

/// <summary>Collects rule violations while validating a document, then reports them as one error.</summary>
internal sealed class Problems
{
    private readonly List<string> _found = [];

    internal void Add(string problem) => _found.Add(problem);

    internal void AddIf(bool broken, string problem)
    {
        if (broken)
        {
            _found.Add(problem);
        }
    }

    internal Result<T> Into<T>(T value, ErrorDefinition error) =>
        _found.Count == 0 ? value : error.With(string.Join("; ", _found));
}
