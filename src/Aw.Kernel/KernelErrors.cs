namespace Aw.Kernel;

/// <summary>The kernel's own error codes: one generic code per <see cref="ErrorKind"/>. Packages define their own.</summary>
public static class KernelErrors
{
    /// <summary>AW-KERNEL-001: the input failed validation.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-KERNEL-001", ErrorKind.Invalid, "Input failed validation");

    /// <summary>AW-KERNEL-002: the thing asked for does not exist.</summary>
    public static readonly ErrorDefinition NotFound = new("AW-KERNEL-002", ErrorKind.NotFound, "Not found");

    /// <summary>AW-KERNEL-003: the operation conflicts with the current state.</summary>
    public static readonly ErrorDefinition Conflict = new("AW-KERNEL-003", ErrorKind.Conflict, "Conflicts with the current state");

    /// <summary>AW-KERNEL-004: a rate limit was hit.</summary>
    public static readonly ErrorDefinition RateLimited = new("AW-KERNEL-004", ErrorKind.RateLimited, "Rate limited");

    /// <summary>AW-KERNEL-005: a budget is spent.</summary>
    public static readonly ErrorDefinition BudgetExceeded = new("AW-KERNEL-005", ErrorKind.BudgetExceeded, "Budget exceeded");

    /// <summary>AW-KERNEL-006: a dependency is not available.</summary>
    public static readonly ErrorDefinition Unavailable = new("AW-KERNEL-006", ErrorKind.Unavailable, "Dependency unavailable");

    /// <summary>AW-KERNEL-007: the operation took longer than allowed.</summary>
    public static readonly ErrorDefinition Timeout = new("AW-KERNEL-007", ErrorKind.Timeout, "Timed out");

    /// <summary>AW-KERNEL-008: a policy or gate refused the operation.</summary>
    public static readonly ErrorDefinition Forbidden = new("AW-KERNEL-008", ErrorKind.Forbidden, "Refused by a policy or gate");

    /// <summary>Every kernel code, in code order.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [Invalid, NotFound, Conflict, RateLimited, BudgetExceeded, Unavailable, Timeout, Forbidden];
}
