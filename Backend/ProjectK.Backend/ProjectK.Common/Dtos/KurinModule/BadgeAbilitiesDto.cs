using System.Text.Json.Serialization;

namespace ProjectK.Common.Dtos.KurinModule;

/// <param name="Review">Confirm or return a submitted вмілість.</param>
public sealed record BadgeAbilitiesDto(bool Review);
