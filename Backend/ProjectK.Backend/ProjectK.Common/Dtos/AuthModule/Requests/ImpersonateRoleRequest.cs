using System.Text.Json.Serialization;
using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Dtos.AuthModule.Requests;

/// <summary>Body of <c>POST api/dev/impersonate</c>: the role to hold, in this kurin or the current one.</summary>
public record ImpersonateRoleRequest([property: JsonRequired] DevRole Role, Guid? KurinKey);
