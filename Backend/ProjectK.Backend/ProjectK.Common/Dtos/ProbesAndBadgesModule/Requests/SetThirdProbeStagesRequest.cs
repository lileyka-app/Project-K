namespace ProjectK.Common.Dtos.ProbesAndBadgesModule.Requests;

/// <summary>The referent's stages, each the day it happened; a null clears it.</summary>
public class SetThirdProbeStagesRequest
{
    public DateOnly? MotivationLetterOn { get; set; }
    public DateOnly? SelfReflectionOn { get; set; }
    public DateOnly? ApplicationSubmittedOn { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public DateOnly? NamedOn { get; set; }
    public DateOnly? ArchivedOn { get; set; }
}
