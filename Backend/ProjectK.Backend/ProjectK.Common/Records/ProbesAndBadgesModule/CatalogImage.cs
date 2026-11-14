namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>An image downloaded from the catalogue.</summary>
public sealed record CatalogImage(byte[] Content, string ContentType);
