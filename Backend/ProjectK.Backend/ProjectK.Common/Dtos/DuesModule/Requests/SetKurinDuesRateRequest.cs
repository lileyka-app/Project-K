namespace ProjectK.Common.Dtos.DuesModule.Requests;

/// <summary>
/// The kurin's статті of the вкладка, from a quarter on, for the whole kurin.
/// <para>
/// A client that does not know статті yet leaves <see cref="Articles"/> out and sends the three
/// amounts instead; they become a «Станиця» and a «Курінь» стаття.
/// </para>
/// </summary>
public sealed class SetKurinDuesRateRequest
{
    public QuarterRequest FromQuarter { get; set; } = new();
    /// <summary>The tag the rate is for, in a kurin without гуртки; null for everyone else.</summary>
    public Guid? TagKey { get; set; }
    public List<DuesArticleRequest>? Articles { get; set; }
    public decimal StanytsiaFull { get; set; }
    public decimal StanytsiaReduced { get; set; }
    public decimal KurinShare { get; set; }
}
