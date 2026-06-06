using Vidarr.Api;
using Vidarr.Tests.Common;

namespace Vidarr.Api.Tests;

public class SessionSignerTests
{
    private static string Secret => Convert.ToBase64String([.. Enumerable.Range(0, 32).Select(i => (byte)i)]);

    [Fact]
    public void Sign_then_verify_round_trips()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var signer = new HmacSessionSigner(clock);
        var token = signer.Sign(Secret, "alice", TimeSpan.FromMinutes(5));
        signer.TryVerify(Secret, token, out var subject).Should().BeTrue();
        subject.Should().Be("alice");
    }

    [Fact]
    public void Verify_rejects_tampered_payload()
    {
        var signer = new HmacSessionSigner(new FakeClock(DateTimeOffset.UtcNow));
        var token = signer.Sign(Secret, "alice", TimeSpan.FromMinutes(5));
        var parts = token.Split('.');
        var tampered = parts[0] + "X." + parts[1];
        signer.TryVerify(Secret, tampered, out _).Should().BeFalse();
    }

    [Fact]
    public void Verify_rejects_expired_token()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var signer = new HmacSessionSigner(clock);
        var token = signer.Sign(Secret, "alice", TimeSpan.FromMinutes(1));
        clock.Advance(TimeSpan.FromMinutes(2));
        signer.TryVerify(Secret, token, out _).Should().BeFalse();
    }

    [Fact]
    public void Verify_rejects_wrong_secret()
    {
        var signer = new HmacSessionSigner(new FakeClock(DateTimeOffset.UtcNow));
        var token = signer.Sign(Secret, "alice", TimeSpan.FromMinutes(5));
        var otherSecret = Convert.ToBase64String([.. Enumerable.Range(100, 32).Select(i => (byte)i)]);
        signer.TryVerify(otherSecret, token, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-dot-here")]
    [InlineData(".empty-payload")]
    [InlineData("payload.")]
    [InlineData("!!notb64!!.!!notb64!!")]
    public void Verify_rejects_malformed_tokens(string token)
    {
        new HmacSessionSigner(new FakeClock(DateTimeOffset.UtcNow))
            .TryVerify(Secret, token, out _).Should().BeFalse();
    }

    [Fact]
    public void Plain_text_secret_is_accepted_via_utf8_fallback()
    {
        var signer = new HmacSessionSigner(new FakeClock(DateTimeOffset.UtcNow));
        const string plainSecret = "this-is-not-base64-but-ok!!";
        var token = signer.Sign(plainSecret, "bob", TimeSpan.FromMinutes(5));
        signer.TryVerify(plainSecret, token, out var subject).Should().BeTrue();
        subject.Should().Be("bob");
    }
}
