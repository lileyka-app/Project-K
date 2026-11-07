using Microsoft.Extensions.Options;
using Moq;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure.Services.EmailService;
using Resend;
using Xunit.Abstractions;

namespace ProjectK.Infrastructure.Tests.Services;

/// <summary>
/// The letters people actually receive: in Ukrainian, under the banner, with the link both as a
/// button and as text, and the address of the sender the settings name.
/// </summary>
public class ResendEmailServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly List<EmailMessage> _sent = [];
    private readonly ResendEmailService _service;

    public ResendEmailServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var resend = new Mock<IResend>();
        resend
            .Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback((EmailMessage message, CancellationToken _) => _sent.Add(message))
            .ReturnsAsync(new ResendResponse<Guid>(Guid.NewGuid(), null!));
        var settings = Options.Create(new EmailSettings
        {
            FromEmail = "lileyka@example.com",
            FromName = "Лілейка",
            ReplyTo = "hello@lileyka.example",
            BaseUrl = "https://lileyka.example"
        });
        _service = new ResendEmailService(resend.Object, settings);
    }

    [Fact]
    public async Task Invitation_ShouldBeInUkrainian_UnderTheBanner_WithTheLinkThreeTimes()
    {
        await _service.SendInvitationEmailAsync("marta@example.com", "tok+en", CancellationToken.None);

        var message = Assert.Single(_sent);
        Assert.Equal("Лілейка <lileyka@example.com>", message.From);
        Assert.Contains("marta@example.com", message.To);
        Assert.Equal("Лілейка · запрошення до системи", message.Subject);
        Assert.Contains("https://lileyka.example/assets/images/email-banner.png", message.HtmlBody);
        // The button, the text link and the visible address: every client shows at least one.
        Assert.Equal(3, CountOf(message.HtmlBody!, "https://lileyka.example/activate/tok+en"));
        Assert.Contains("Активувати акаунт", message.HtmlBody);
        Assert.Contains("«Спам»", message.HtmlBody);
        Assert.DoesNotContain("Welcome", message.HtmlBody);
        Assert.Equal("hello@lileyka.example", message.ReplyTo);
        // The text part says the same and carries the link, so an HTML-only letter never leaves.
        Assert.Contains("Активувати акаунт: https://lileyka.example/activate/tok+en", message.TextBody);
        Assert.Contains("Вас запрошено до Лілейки", message.TextBody);
        Assert.DoesNotContain("<", message.TextBody);
        _output.WriteLine("INVITATION-HTML-BEGIN");
        _output.WriteLine(message.HtmlBody);
        _output.WriteLine("INVITATION-HTML-END");
    }

    [Fact]
    public async Task PasswordReset_ShouldEncodeTheTokenAndAddress_AndSpeakUkrainian()
    {
        await _service.SendPasswordResetEmailAsync("marta+plast@example.com", "a+b/c=", CancellationToken.None);

        var message = Assert.Single(_sent);
        Assert.Equal("Лілейка · відновлення пароля", message.Subject);
        Assert.Contains("reset-password?token=a%2Bb%2Fc%3D&amp;email=marta%2Bplast%40example.com", message.HtmlBody);
        Assert.Contains("Встановити новий пароль", message.HtmlBody);
        Assert.DoesNotContain("Password Reset", message.HtmlBody);
        Assert.Contains("reset-password?token=a%2Bb%2Fc%3D&email=marta%2Bplast%40example.com", message.TextBody);
    }

    [Fact]
    public async Task EmailChange_ShouldNameBothAddresses_InTheSameFrame()
    {
        await _service.SendEmailChangeConfirmationEmailAsync("new@example.com", "old@example.com", "https://lileyka.example/settings/account?confirmEmail=true&token=t", CancellationToken.None);

        var message = Assert.Single(_sent);
        Assert.Equal("Лілейка · підтвердження зміни пошти", message.Subject);
        Assert.Contains("old@example.com", message.HtmlBody);
        Assert.Contains("new@example.com", message.HtmlBody);
        Assert.Contains("email-banner.png", message.HtmlBody);
        Assert.Contains("Підтвердити пошту: https://lileyka.example/settings/account?confirmEmail=true&token=t", message.TextBody);
        Assert.DoesNotContain("Confirm email", message.HtmlBody);
    }

    // Staging sends every letter to Resend's sink: it shows in the Resend log, with the person it
    // was meant for in the subject and a header, and reaches nobody.
    [Fact]
    public async Task WithRedirect_ShouldSendToTheSink_AndKeepTheRealRecipientVisible()
    {
        var sent = new List<EmailMessage>();
        var resend = new Mock<IResend>();
        resend
            .Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback((EmailMessage message, CancellationToken _) => sent.Add(message))
            .ReturnsAsync(new ResendResponse<Guid>(Guid.NewGuid(), null!));
        var service = new ResendEmailService(resend.Object, Options.Create(new EmailSettings
        {
            FromEmail = "a@example.com",
            BaseUrl = "https://lileyka.example",
            RedirectAllTo = "delivered@resend.dev"
        }));

        await service.SendInvitationEmailAsync("marta@example.com", "t", CancellationToken.None);

        var message = Assert.Single(sent);
        Assert.Equal(["delivered@resend.dev"], message.To.ToArray());
        Assert.Equal("Лілейка · запрошення до системи → marta@example.com", message.Subject);
        Assert.Equal("marta@example.com", message.Headers!["X-Original-To"]);
        Assert.Contains("/activate/t", message.HtmlBody);
    }

    [Fact]
    public async Task WaitlistSubmitted_ShouldNameTheApplicant_AndLeadToTheWaitlist()
    {
        await _service.SendWaitlistSubmittedEmailAsync("admin@example.com", "Марта Коваль", "курінь 97", CancellationToken.None);

        var message = Assert.Single(_sent);
        Assert.Equal("Лілейка · нова заявка на розгляд", message.Subject);
        Assert.Contains("Марта Коваль (курінь 97)", message.HtmlBody);
        Assert.Contains("Переглянути заявки: https://lileyka.example/waitlist", message.TextBody);
    }

    [Fact]
    public async Task WithoutReplyTo_ShouldLeaveTheHeaderOut()
    {
        var sent = new List<EmailMessage>();
        var resend = new Mock<IResend>();
        resend
            .Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback((EmailMessage message, CancellationToken _) => sent.Add(message))
            .ReturnsAsync(new ResendResponse<Guid>(Guid.NewGuid(), null!));
        var service = new ResendEmailService(resend.Object, Options.Create(new EmailSettings { FromEmail = "a@example.com", BaseUrl = "https://lileyka.example" }));

        await service.SendInvitationEmailAsync("marta@example.com", "t", CancellationToken.None);

        Assert.Null(Assert.Single(sent).ReplyTo);
    }

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        for (var index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
