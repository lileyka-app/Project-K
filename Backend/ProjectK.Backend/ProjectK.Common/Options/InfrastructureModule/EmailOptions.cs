namespace ProjectK.Common.Options.InfrastructureModule;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Mock"; // "Mock" or "Resend"
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "onboarding@resend.dev";
    public string FromName { get; set; } = "Лілейка";
    /// <summary>A mailbox a person can answer to; empty leaves the letters without Reply-To.</summary>
    public string ReplyTo { get; set; } = string.Empty;
    /// <summary>
    /// When set, every letter goes to this address instead of the real one, which stays in the
    /// subject and in X-Original-To. Staging points it at Resend's delivered@resend.dev: the letter
    /// shows in the Resend log with its links and tokens and reaches no mailbox.
    /// </summary>
    public string RedirectAllTo { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "http://localhost:4200";
}
