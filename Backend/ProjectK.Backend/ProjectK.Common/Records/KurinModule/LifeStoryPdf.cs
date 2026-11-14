namespace ProjectK.Common.Records.KurinModule;

/// <summary>The rendered life story and the name it downloads under.</summary>
public sealed record LifeStoryPdf(byte[] Content, string FileName);
