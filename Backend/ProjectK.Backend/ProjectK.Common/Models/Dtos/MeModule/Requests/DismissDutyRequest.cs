namespace ProjectK.Common.Models.Dtos.MeModule.Requests;

/// <summary>Hide one duty from «Справи»: its key and fingerprint, as the duties read gave them.</summary>
public sealed class DismissDutyRequest
{
    public string Key { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
}
