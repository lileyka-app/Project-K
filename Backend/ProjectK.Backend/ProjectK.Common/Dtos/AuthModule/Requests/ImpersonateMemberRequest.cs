using System.Text.Json.Serialization;

namespace ProjectK.Common.Dtos.AuthModule.Requests;

/// <summary>Body of <c>POST api/dev/impersonate/member</c>: the person to sign in as.</summary>
public record ImpersonateMemberRequest([property: JsonRequired] Guid MemberKey);
