namespace Aw.Kernel.Testing;

/// <summary>One test that breaks a lane rule in a CTRF report (spec §4b.5).</summary>
/// <param name="RuleId">'LANE-TEST-001' (over its lane's per-test cap) or 'LANE-TEST-002' (not exactly one known lane).</param>
/// <param name="Package">The test assembly, for example 'Aw.Kernel.Data.Tests'.</param>
/// <param name="Test">The test's full name.</param>
/// <param name="Message">The measured value, the limit and the fix, for people and CI logs.</param>
public sealed record LaneCapViolation(string RuleId, string Package, string Test, string Message);
