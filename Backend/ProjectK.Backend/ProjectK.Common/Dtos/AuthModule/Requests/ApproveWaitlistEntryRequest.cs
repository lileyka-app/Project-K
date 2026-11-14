namespace ProjectK.Common.Dtos.AuthModule.Requests;

/// <summary>Body of <c>POST api/auth/onboarding/waitlist/{key}/approve</c>.</summary>
public sealed class ApproveWaitlistEntryRequest
{
    public string? KraiCode { get; set; }
}
