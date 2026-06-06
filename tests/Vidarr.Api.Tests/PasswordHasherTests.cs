using Vidarr.Api;

namespace Vidarr.Api.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_then_verify_round_trips()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var encoded = hasher.Hash("hunter2");
        hasher.Verify("hunter2", encoded).Should().BeTrue();
        hasher.Verify("Hunter2", encoded).Should().BeFalse();
    }

    [Fact]
    public void Hash_includes_random_salt()
    {
        var hasher = new Pbkdf2PasswordHasher();
        hasher.Hash("same").Should().NotBe(hasher.Hash("same"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("v1$wrong$x$y")]
    [InlineData("v2$100000$AAAA$BBBB")] // bad prefix
    [InlineData("v1$abc$AAAA$BBBB")] // bad iters
    [InlineData("v1$100000$$$$")] // empty fields
    public void Verify_rejects_malformed_encodings(string encoded)
    {
        new Pbkdf2PasswordHasher().Verify("anything", encoded).Should().BeFalse();
    }

    [Fact]
    public void Verify_rejects_invalid_base64()
    {
        new Pbkdf2PasswordHasher().Verify("p", "v1$100000$!!notb64!!$xxx").Should().BeFalse();
    }
}
