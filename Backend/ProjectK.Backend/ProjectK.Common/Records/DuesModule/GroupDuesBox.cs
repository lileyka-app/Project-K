namespace ProjectK.Common.Records.DuesModule;

/// <summary>A гурток's box, and the transfers to the kurin that have not been confirmed yet.</summary>
public sealed record GroupDuesBox(Guid GroupKey, DuesCashBox Box, decimal InTransit);
