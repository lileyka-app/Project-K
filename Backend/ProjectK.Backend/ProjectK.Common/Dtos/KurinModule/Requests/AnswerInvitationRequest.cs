namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>POST api/me/invitations/{membershipRequestKey}/answer</c>.</summary>
public sealed class AnswerInvitationRequest
{
    public bool Accept { get; set; }
}
