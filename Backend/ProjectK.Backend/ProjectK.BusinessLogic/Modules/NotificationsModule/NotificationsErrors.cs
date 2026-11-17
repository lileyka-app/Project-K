namespace ProjectK.BusinessLogic.Modules.NotificationsModule;

/// <summary>The codes the inbox, push and feedback requests fail with; the client picks its wording by them.</summary>
public static class NotificationsErrors
{
    /// <summary>The request carries no signed-in person.</summary>
    public const string Unauthorized = "Unauthorized";

    /// <summary>A problem report or screenshot sent without a signed-in person.</summary>
    public const string SignInRequired = "SignInRequired";

    public const string NotificationNotFound = "NotificationNotFound";

    /// <summary>The server has no push keys configured, so it accepts no devices.</summary>
    public const string PushNotConfigured = "PushNotConfigured";

    public const string DescriptionRequired = "DescriptionRequired";
    public const string DescriptionTooLong = "DescriptionTooLong";
    public const string FieldTooLong = "FieldTooLong";
    public const string TooManyScreenshots = "TooManyScreenshots";

    /// <summary>The tracker did not take the report; the text is in the log.</summary>
    public const string ReportNotDelivered = "ReportNotDelivered";

    public const string InvalidImage = "InvalidImage";
}
