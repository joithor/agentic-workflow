using Aw.Sindri.Core;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Tests.Secrets;

public sealed class SecretPointerParseTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("env:ONYX_TOKEN", SecretScheme.Env, "ONYX_TOKEN")]
    [InlineData("env:_x1", SecretScheme.Env, "_x1")]
    [InlineData("file:/home/me/.onyx-token", SecretScheme.File, "/home/me/.onyx-token")]
    [InlineData("keychain:onyx/sindri-read", SecretScheme.Keychain, "onyx/sindri-read")]
    [InlineData("op:Work/Onyx/credential", SecretScheme.OnePassword, "Work/Onyx/credential")]
    public void A_valid_pointer_parses(string text, SecretScheme scheme, string target)
    {
        var pointer = SecretPointer.Parse(text);

        Assert.True(pointer.IsSuccess);
        Assert.Equal(scheme, pointer.Value.Scheme);
        Assert.Equal(target, pointer.Value.Target);
        Assert.Equal(text, pointer.Value.ToString());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("on_rawsecret")]
    [InlineData("")]
    [InlineData("vault:a/b")]
    [InlineData("env:")]
    [InlineData("env:1BAD")]
    [InlineData("env:BAD-NAME")]
    [InlineData("file:relative/token")]
    [InlineData("file:")]
    [InlineData("keychain:onyx")]
    [InlineData("keychain:onyx/a/b")]
    [InlineData("keychain:/account")]
    [InlineData("keychain:service/")]
    [InlineData("keychain:-s/account")]
    [InlineData("keychain:service/-w")]
    [InlineData("op:vault/item")]
    [InlineData("op:vault//field")]
    [InlineData("op:-vault/item/field")]
    [InlineData("op:vault/item/field/extra")]
    public void An_invalid_pointer_is_SND_SECRET_001(string text)
    {
        var pointer = SecretPointer.Parse(text);

        Assert.True(pointer.IsError);
        Assert.Equal(SindriErrors.SecretPointerInvalid.Code, pointer.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_pointer_with_a_nul_is_SND_SECRET_001()
        => Assert.Equal(SindriErrors.SecretPointerInvalid.Code, SecretPointer.Parse("file:/tmp/a\0b").Error.Code);

    [Theory, Trait("Lane", "unit")]
    [InlineData("on_rawsecret", "on_rawsecret")]
    [InlineData("env:sk-live-SECRET-VALUE", "sk-live-SECRET-VALUE")]
    [InlineData("keychain:hunter2", "hunter2")]
    [InlineData("op:p4ssw0rd", "p4ssw0rd")]
    [InlineData("file:p4ssw0rd", "p4ssw0rd")]
    public void The_error_never_repeats_the_text_after_the_scheme(string text, string secret)
    {
        var message = SecretPointer.Parse(text).Error.Message;

        Assert.DoesNotContain(secret, message, StringComparison.Ordinal);
        Assert.Contains("keychain:<service>/<account>", message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_scheme_that_looks_like_a_secret_isnt_repeated_either()
        => Assert.DoesNotContain("sk-9f8e7d6c5b4a", SecretPointer.Parse("sk-9f8e7d6c5b4a:rest").Error.Message, StringComparison.Ordinal);

    [Theory, Trait("Lane", "unit")]
    [InlineData("env:ONYX_TOKEN", true)]
    [InlineData("keychain:onyx/sindri-read", true)]
    [InlineData("AW_STATE_DIR", false)]
    [InlineData("LINEAR_API_KEY", false)]
    public void A_name_with_a_colon_is_a_pointer(string name, bool isPointer)
        => Assert.Equal(isPointer, SecretPointer.IsPointer(name));
}
