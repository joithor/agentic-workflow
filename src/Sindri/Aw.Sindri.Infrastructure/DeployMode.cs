namespace Aw.Sindri.Infrastructure;

/// <summary>How this Sindri is deployed (deployable-context spec A2a): on a person's machine, or as the pod the image runs in.</summary>
internal enum DeployMode
{
    /// <summary>Today's behaviour; nothing sets anything.</summary>
    Desktop,

    /// <summary>AW_SINDRI_DEPLOY=pod. Only the switch exists so far; later changes key on it.</summary>
    Pod,
}
