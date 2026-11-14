using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.AuthModule.Requests;

/// <summary>Body of <c>POST api/dev/impersonate</c>: the role to hold, in this kurin or the current one.</summary>
public record ImpersonateRoleRequest([property: JsonRequired] DevRole Role, Guid? KurinKey);
