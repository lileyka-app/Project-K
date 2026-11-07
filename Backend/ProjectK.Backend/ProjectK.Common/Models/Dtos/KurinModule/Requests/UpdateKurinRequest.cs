using System.ComponentModel.DataAnnotations;

namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

public class UpdateKurinRequest
{
    /// <summary>The kurin's number; null makes it a підготовчий курінь.</summary>
    public int? Number { get; set; }

    /// <summary>Точкування on or off; left out, the kurin keeps what it has.</summary>
    public bool? ScoreEnabled { get; set; }

    /// <summary>The kurin's own role names by role («Pysar» → «Скриптор»); left out, they stay as they are.</summary>
    public Dictionary<string, string?>? RoleNames { get; set; }

    [MaxLength(120)]
    public string? Stanytsia { get; set; }

    [MaxLength(120)]
    public string? RegionOrCountry { get; set; }

    [MaxLength(200)]
    public string? NamedAfter { get; set; }

    [MaxLength(4000)]
    public string? Description { get; set; }

    public bool ProfileVerificationEnabled { get; set; }
}
