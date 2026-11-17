namespace ProjectK.Common.Records.DuesModule;

/// <summary>A quarter's вкладка, split by where the money goes. The край part is last so a kurin without one reads as before.</summary>
public sealed record DuesAmount(decimal Stanytsia, decimal Kurin, decimal Group, decimal Krai = 0)
{
    public static DuesAmount Zero { get; } = new(0, 0, 0);

    public decimal Total => Stanytsia + Krai + Kurin + Group;

    /// <summary>What the гурток hands up to the kurin: everything but its own part.</summary>
    public decimal Up => Stanytsia + Krai + Kurin;
}
