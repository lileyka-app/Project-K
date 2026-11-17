namespace ProjectK.Common.Dtos.IdentityModule.Requests;

/// <summary>Body of <c>POST api/dev/return</c>: the ticket an impersonation handed out.</summary>
public record ReturnFromImpersonationRequest(string Ticket);
