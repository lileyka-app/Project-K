namespace ProjectK.Common.Exceptions;

/// <summary>
/// A save found a row changed by another request since it was read: its concurrency token no longer
/// matches. Nothing of the save was written.
/// </summary>
public sealed class StaleRowException : Exception
{
    public StaleRowException(Exception innerException)
        : base("The row was changed by another request since it was read.", innerException)
    {
    }
}
