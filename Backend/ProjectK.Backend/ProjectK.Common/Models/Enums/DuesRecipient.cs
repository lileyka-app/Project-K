namespace ProjectK.Common.Models.Enums;

/// <summary>
/// Where the money of a стаття of the вкладка goes. Stored as a number.
/// <para>
/// <see cref="Stanytsia"/> and <see cref="Krai"/> are collected for someone else and forwarded, each
/// with its own transfer; <see cref="Kurin"/> stays in the kurin's box. A край fee set before
/// <see cref="Krai"/> existed was a станиця стаття and stays one until the kurin moves it.
/// </para>
/// </summary>
public enum DuesRecipient
{
    Stanytsia = 0,
    Kurin = 1,
    Krai = 2
}
