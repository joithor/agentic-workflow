namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class SecretNameTests
{
    [Theory]
    [InlineData("client_secret")]
    [InlineData("X-Api-Key")]
    [InlineData("accessToken")]
    [InlineData("GITHUB_TOKEN")]
    [InlineData("db.password")]
    [InlineData("Authorization")]
    [InlineData("proxy-authorization")]
    [InlineData("Cookie")]
    [InlineData("set-cookie")]
    [InlineData("http.request.header.cookie")]
    [InlineData("http.request.header.authorization")]
    [InlineData("http.response.header.set-cookie")]
    public void A_name_that_holds_a_secret_is_recognized(string name) =>
        Assert.True(Redactor.Default.IsSecretName(name));

    [Theory]
    [InlineData("user_name")]
    [InlineData("token_count")]
    [InlineData("max_tokens")]
    [InlineData("secretary")]
    [InlineData("")]
    public void An_ordinary_name_is_not(string name) =>
        Assert.False(Redactor.Default.IsSecretName(name));
}
