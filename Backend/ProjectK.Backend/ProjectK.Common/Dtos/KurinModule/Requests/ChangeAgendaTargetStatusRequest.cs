using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body for moving one target; <c>MemberKey</c> names whose part, in «кожному окремо».</summary>
public sealed record ChangeAgendaTargetStatusRequest([property: JsonRequired] AgendaItemStatus Status, Guid? MemberKey);
