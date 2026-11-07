using System.Security.Cryptography;

namespace ProjectK.Common.Models.Records;

/// <summary>
/// The code a person reads out or pastes so another kurin can find them without searching by name.
/// <para>
/// Drawn at random and stored, never derived from the member's key. Until 2026-10-30 it was the
/// first ten hex digits of <c>MemberKey</c> — and that key sits in the address of every card the
/// провід opens, so anyone who could see the person could work their code out. Only the person
/// themself may hand the code over (JOIN-03); a code nobody else can compute is what makes that true.
/// Hexadecimal keeps it unambiguous out loud — no O against 0, no I against 1.
/// </para>
/// </summary>
public static class MemberPublicId
{
    public const string Prefix = "PL";

    /// <summary>A fresh code, e.g. <c>PL-4F7A2-9D4A1</c>: 40 random bits.</summary>
    public static string New()
    {
        var digits = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
        return $"{Prefix}-{digits[..5]}-{digits[5..10]}";
    }
}
