using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

/// <summary>Body for an RSVP set; <c>OccurrenceStartUtc</c> names the occurrence of a series, null for a one-off event.</summary>
public sealed record SetAgendaResponseRequest([property: JsonRequired] AgendaRsvpStatus Status, DateTime? OccurrenceStartUtc = null);
