namespace ProjectK.Common.Models.Dtos.MeModule;

/// <summary>A QR and the address behind it; <see cref="ExpiresAtUtc"/> is set when it stops working.</summary>
public sealed record CardCodeDto(string Url, string QrCode, DateTime? ExpiresAtUtc);
