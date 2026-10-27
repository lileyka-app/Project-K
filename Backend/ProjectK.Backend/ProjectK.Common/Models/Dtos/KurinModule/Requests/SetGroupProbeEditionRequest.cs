using System.ComponentModel.DataAnnotations;

namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

public class SetGroupProbeEditionRequest
{
    /// <summary>The catalogue's edition, e.g. "upyu-2026"; null or empty for the unnamed one.</summary>
    [MaxLength(64)]
    public string? Edition { get; set; }
}
