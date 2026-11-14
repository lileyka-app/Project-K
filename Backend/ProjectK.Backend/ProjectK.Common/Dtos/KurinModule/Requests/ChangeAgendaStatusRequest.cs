using System.Text.Json.Serialization;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body for the board status move.</summary>
public sealed record ChangeAgendaStatusRequest([property: JsonRequired] AgendaItemStatus Status);
