using System.Text.Json.Serialization;
using ProjectK.Common.Records.AuthModule;

namespace ProjectK.Common.Dtos.AuthModule;

/// <summary>
/// A sign-in: who signed in, what they may do in the kurin they are in, and the tokens. Every way of
/// getting a session answers with it — the password step, the second factor, activation, a kurin
/// switch, a saved-account switch, the demo seats and the dev role switcher.
/// </summary>
public sealed record LoginUserDto
{
    public required Guid UserKey { get; init; }
    public required Guid? MemberKey { get; init; }
    public required string Email { get; init; }
    public required bool IsAdmin { get; init; }
    public required IReadOnlyCollection<string> Permissions { get; init; }
    public required IReadOnlyCollection<string> Roles { get; init; }
    public required string? KurinKey { get; init; }
    public required bool RequiresMfa { get; init; }

    /// <summary>
    /// Proof that the password step passed, handed back only when <see cref="RequiresMfa"/> is
    /// set. The second-factor step has to bring it back; without it a code alone is refused.
    /// </summary>
    public string? MfaToken { get; init; }

    public required JwtResponse? Tokens { get; init; }

    /// <summary>
    /// Issued when the second factor just passed. The controller turns it into an HttpOnly cookie
    /// and it never reaches the JSON body: a script must not be able to read what makes a device trusted.
    /// </summary>
    [JsonIgnore]
    public MfaTrustGrant? MfaTrust { get; init; }
}
