using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.MeModule;

/// <summary>A QR and the address behind it; <see cref="ExpiresAtUtc"/> is set when it stops working.</summary>
public sealed record CardCodeDto(string Url, string QrCode, DateTime? ExpiresAtUtc);
