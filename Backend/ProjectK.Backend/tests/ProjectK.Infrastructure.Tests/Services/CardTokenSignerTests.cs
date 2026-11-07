using Microsoft.Extensions.Configuration;
using ProjectK.Infrastructure.Services;
using Xunit;

namespace ProjectK.Infrastructure.Tests.Services;

/// <summary>A «перевірити» token names its person until it expires, and nobody can make or alter one.</summary>
public class CardTokenSignerTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private static CardTokenSigner Signer(string key = "a-test-key-long-enough-for-hmac-signing-0123456789") =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = key }).Build());

    [Fact]
    public void ATokenNamesItsPerson_UntilItExpires()
    {
        var person = Guid.NewGuid();
        var token = Signer().Sign(person, Now.AddMinutes(3));

        Assert.True(token.Length <= 43);
        Assert.Equal(person, Signer().Read(token, Now));
        Assert.Null(Signer().Read(token, Now.AddMinutes(3)));
    }

    [Fact]
    public void AnAlteredOrForeignToken_IsRefused()
    {
        var token = Signer().Sign(Guid.NewGuid(), Now.AddMinutes(3));
        var altered = (token[0] == 'A' ? 'B' : 'A') + token[1..];

        Assert.Null(Signer().Read(altered, Now));
        Assert.Null(Signer("another-key-long-enough-for-hmac-signing-9876543210").Read(token, Now));
        Assert.Null(Signer().Read("not a token", Now));
        Assert.Null(Signer().Read(string.Empty, Now));
    }
}
