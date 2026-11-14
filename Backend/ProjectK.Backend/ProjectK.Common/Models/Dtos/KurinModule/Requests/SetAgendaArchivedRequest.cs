using System.Text.Json.Serialization;

namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

/// <summary>Body for archiving a task (true) or taking it back (false).</summary>
public sealed record SetAgendaArchivedRequest([property: JsonRequired] bool Archived);
