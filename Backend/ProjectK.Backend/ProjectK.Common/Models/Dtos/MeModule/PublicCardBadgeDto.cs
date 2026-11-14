using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.MeModule;

public sealed record PublicCardBadgeDto(string Title, string? ImagePath, DateTime? ConfirmedAtUtc);
