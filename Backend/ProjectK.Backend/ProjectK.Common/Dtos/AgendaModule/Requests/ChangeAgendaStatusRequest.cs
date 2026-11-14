using System.Text.Json.Serialization;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Dtos.AgendaModule.Requests;

/// <summary>Body for the board status move.</summary>
public sealed record ChangeAgendaStatusRequest([property: JsonRequired] AgendaItemStatus Status);
