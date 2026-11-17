using System.Text.Json.Serialization;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Dtos.AgendaModule.Requests;

/// <summary>Body for moving one target; <c>MemberKey</c> names whose part, in «кожному окремо».</summary>
public sealed record ChangeAgendaTargetStatusRequest([property: JsonRequired] AgendaItemStatus Status, Guid? MemberKey);
