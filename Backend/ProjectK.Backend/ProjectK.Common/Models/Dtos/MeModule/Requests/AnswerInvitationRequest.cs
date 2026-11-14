namespace ProjectK.Common.Models.Dtos.MeModule.Requests;

/// <summary>Body of <c>POST api/me/invitations/{membershipRequestKey}/answer</c>.</summary>
public sealed class AnswerInvitationRequest
{
    public bool Accept { get; set; }
}
