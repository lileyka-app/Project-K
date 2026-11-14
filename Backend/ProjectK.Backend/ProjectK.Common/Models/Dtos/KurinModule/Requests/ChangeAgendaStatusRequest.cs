using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

/// <summary>Body for the board status move.</summary>
public sealed record ChangeAgendaStatusRequest([property: JsonRequired] AgendaItemStatus Status);
