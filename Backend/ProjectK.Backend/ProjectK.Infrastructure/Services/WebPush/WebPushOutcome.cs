namespace ProjectK.Infrastructure.Services.WebPush;

/// <summary>What became of one push.</summary>
public enum WebPushOutcome
{
    Delivered,

    /// <summary>The push service no longer knows the device (404/410): its row should go.</summary>
    Gone,

    /// <summary>Anything else — refused, throttled, unreachable. Logged; the row stays.</summary>
    Failed
}
