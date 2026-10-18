using System.ComponentModel.DataAnnotations;

namespace ProjectK.Common.Entities.KraiModule;

/// <summary>
/// A пластовий край. Every kurin belongs to one, and the край's profile decides what its kurins
/// see: which вмілості and проби, the currency, the time zone. One instance holds as many краї
/// as it needs.
/// </summary>
public class Krai
{
    public const string Ukraine = "ua";
    public const string Czechia = "cz";
    public const string Poland = "pl";

    /// <summary>A short code, the same one the catalogue marks its entries with (<c>ua</c>, <c>cz</c>).</summary>
    [Key]
    [MaxLength(8)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<KraiProfileRevision> ProfileRevisions { get; set; } = new List<KraiProfileRevision>();

    /// <summary>Codes are stored lower-case; a code from a form is read the same way, and a blank one as none.</summary>
    public static string? NormalizeCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToLowerInvariant();
}
