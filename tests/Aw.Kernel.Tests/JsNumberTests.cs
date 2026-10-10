using Aw.Kernel.Json;

namespace Aw.Kernel.Tests;

/// <summary>Number#toString as Node prints it (ECMAScript Number::toString), from .NET's shortest round-trip digits.</summary>
[Trait("Lane", "unit")]
public sealed class JsNumberTests
{
    [Theory]
    [InlineData(0d, "0")]
    [InlineData(-1d, "-1")]
    [InlineData(1e21, "1e+21")]
    [InlineData(1e20, "100000000000000000000")]
    [InlineData(1.5e21, "1.5e+21")]
    [InlineData(1e-6, "0.000001")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(-1.25e-7, "-1.25e-7")]
    [InlineData(123.456, "123.456")]
    [InlineData(5e-324, "5e-324")]
    [InlineData(1.7976931348623157e308, "1.7976931348623157e+308")]
    [InlineData(0.30000000000000004, "0.30000000000000004")]
    public void Formats_like_Node(double value, string expected) =>
        Assert.Equal(expected, JsNumber.Format(value));

    [Fact]
    public void Negative_zero_prints_as_zero() => Assert.Equal("0", JsNumber.Format(-0d));
}
