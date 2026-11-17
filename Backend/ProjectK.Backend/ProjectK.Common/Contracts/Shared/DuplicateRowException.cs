namespace ProjectK.Common.Contracts.Shared;

/// <summary>
/// A save hit a unique index: another request wrote the same row first. The rows this save tried to
/// add are no longer tracked, so the caller may re-read and try again, or report the conflict.
/// </summary>
public sealed class DuplicateRowException : Exception
{
    public DuplicateRowException(Exception innerException)
        : base("A row with the same unique key already exists.", innerException)
    {
    }
}
