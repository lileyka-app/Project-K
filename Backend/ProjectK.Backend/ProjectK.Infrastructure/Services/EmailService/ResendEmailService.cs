using System.Net;
using Microsoft.Extensions.Options;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using Resend;

namespace ProjectK.Infrastructure.Services.EmailService;

/// <summary>
/// The letters Лілейка sends, in Ukrainian, under one frame: the banner on top, one green button,
/// a plain copy of the link for clients that strip buttons, and a footer that says why the letter
/// came. Every colour is inline, because mail clients read no stylesheet. Each letter also carries
/// a plain-text part with the same words and link: spam filters score an HTML-only message down,
/// and a text-only client still gets the whole letter.
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly EmailSettings _settings;

    public ResendEmailService(IResend resend, IOptions<EmailSettings> settings)
    {
        _resend = resend;
        _settings = settings.Value;
    }

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        return SendAsync(to, subject, body, textBody: null, cancellationToken);
    }

    private async Task SendAsync(string to, string subject, string htmlBody, string? textBody, CancellationToken cancellationToken)
    {
        var message = new EmailMessage();
        message.From = $"{_settings.FromName} <{_settings.FromEmail}>";
        if (string.IsNullOrWhiteSpace(_settings.RedirectAllTo))
        {
            message.To.Add(to);
            message.Subject = subject;
        }
        else
        {
            message.To.Add(_settings.RedirectAllTo);
            message.Subject = $"{subject} → {to}";
            message.Headers = new Dictionary<string, string> { ["X-Original-To"] = to };
        }
        message.HtmlBody = htmlBody;
        message.TextBody = textBody;
        if (!string.IsNullOrWhiteSpace(_settings.ReplyTo))
        {
            message.ReplyTo = _settings.ReplyTo;
        }

        await _resend.EmailSendAsync(message, cancellationToken);
    }

    public async Task SendInvitationEmailAsync(string to, string token, CancellationToken cancellationToken = default)
    {
        var activationUrl = $"{_settings.BaseUrl}/activate/{token}";
        var letter = new Letter(
            Title: "Вас запрошено до Лілейки",
            Paragraphs:
            [
                "Для вас створено акаунт у Лілейці — системі обліку куреня. Щоб активувати його і встановити пароль, натисніть кнопку нижче.",
                $"Посилання діє {OnboardingPolicy.InvitationLifetimeDays} днів. Якщо воно прострочиться, нове можна замовити на сторінці входу через «Забули пароль».",
            ],
            ButtonText: "Активувати акаунт",
            Url: activationUrl,
            Footer: "Якщо ви не чекали цього листа, просто не звертайте на нього уваги — без активації акаунт не запрацює.");

        await SendAsync(to, "Лілейка · запрошення до системи", Frame(letter), PlainText(letter), cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string to, string token, CancellationToken cancellationToken = default)
    {
        // Both encoded: a reset token carries '+' and '/', and an address may too, and either
        // one read back from the query string as a space breaks the link for exactly that person.
        var resetUrl = $"{_settings.BaseUrl}/reset-password?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(to)}";
        var letter = new Letter(
            Title: "Відновлення пароля",
            Paragraphs:
            [
                "Ми отримали запит на зміну пароля до вашого акаунта в Лілейці. Щоб обрати новий пароль, натисніть кнопку нижче.",
                "Посилання діє обмежений час.",
            ],
            ButtonText: "Встановити новий пароль",
            Url: resetUrl,
            Footer: "Якщо ви не просили змінити пароль, нічого робити не треба — ваш пароль лишається чинним.");

        await SendAsync(to, "Лілейка · відновлення пароля", Frame(letter), PlainText(letter), cancellationToken);
    }

    public async Task SendEmailChangeConfirmationEmailAsync(string to, string currentEmail, string confirmationUrl, CancellationToken cancellationToken = default)
    {
        var letter = new Letter(
            Title: "Підтвердження зміни пошти",
            Paragraphs:
            [
                $"Ми отримали запит змінити пошту вашого акаунта в Лілейці з {currentEmail} на {to}. Щоб підтвердити нову адресу, натисніть кнопку нижче.",
                "Поки ви не підтвердите, вхід лишається за старою адресою.",
            ],
            ButtonText: "Підтвердити пошту",
            Url: confirmationUrl,
            Footer: "Якщо ви не просили змінити пошту, нічого робити не треба — акаунт лишається за старою адресою.");

        await SendAsync(to, "Лілейка · підтвердження зміни пошти", Frame(letter), PlainText(letter), cancellationToken);
    }

    public async Task SendWaitlistSubmittedEmailAsync(string to, string applicantName, string? claimedKurin, CancellationToken cancellationToken = default)
    {
        var kurin = string.IsNullOrWhiteSpace(claimedKurin) ? "курінь не вказано" : claimedKurin;
        var letter = new Letter(
            Title: "Нова заявка на розгляд",
            Paragraphs:
            [
                $"{applicantName} ({kurin}) подає заявку на Лілейку. Заявка чекає на ваше рішення: схвалити і надіслати запрошення або відхилити.",
                "Поки заявку не розглянуто, людина не може увійти в систему.",
            ],
            ButtonText: "Переглянути заявки",
            Url: $"{_settings.BaseUrl}/waitlist",
            Footer: "Цей лист приходить кожному адміністратору Лілейки, коли зʼявляється нова заявка.");

        await SendAsync(to, "Лілейка · нова заявка на розгляд", Frame(letter), PlainText(letter), cancellationToken);
    }

    public async Task SendKurinBranchRequestEmailAsync(string to, string kurin, string wanted, CancellationToken cancellationToken = default)
    {
        var letter = new Letter(
            Title: "Запит на зміну гілок куреня",
            Paragraphs:
            [
                $"{kurin} просить змінити свої гілки й числа: {wanted}.",
                "Запит чекає на ваше рішення поруч із заявками: схвалити або відхилити.",
            ],
            ButtonText: "Переглянути запити",
            Url: $"{_settings.BaseUrl}/waitlist",
            Footer: "Цей лист приходить кожному адміністратору Лілейки, коли курінь надсилає такий запит.");

        await SendAsync(to, "Лілейка · запит на зміну гілок куреня", Frame(letter), PlainText(letter), cancellationToken);
    }

    /// <summary>What every letter says: a title, the paragraphs, one button with its link, a footer.</summary>
    private sealed record Letter(string Title, IReadOnlyList<string> Paragraphs, string ButtonText, string Url, string Footer);

    /// <summary>The same letter without markup: the words, the link on its own line, the footer.</summary>
    private static string PlainText(Letter letter)
    {
        var lines = new List<string> { letter.Title, string.Empty };
        lines.AddRange(letter.Paragraphs.Select(paragraph => paragraph + Environment.NewLine));
        lines.Add($"{letter.ButtonText}: {letter.Url}");
        lines.Add(string.Empty);
        lines.Add("Не бачите наших листів? Перевірте теку «Спам» і додайте адресу відправника до контактів.");
        lines.Add(string.Empty);
        lines.Add(letter.Footer);
        lines.Add("Лілейка · система обліку куреня. Лист надіслано автоматично.");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// One frame for every letter. Table layout on purpose: it is the only thing every mail
    /// client lays out the same way.
    /// </summary>
    private string Frame(Letter letter)
    {
        var (title, paragraphs, buttonText, url, footer) = letter;
        var bannerUrl = $"{_settings.BaseUrl}/assets/images/email-banner.png";
        var encodedUrl = WebUtility.HtmlEncode(url);
        var text = string.Join(
            string.Empty,
            paragraphs.Select(paragraph =>
                $"<p style='margin:0 0 16px;font-size:16px;line-height:1.5;color:#1F2A27;'>{WebUtility.HtmlEncode(paragraph)}</p>"));

        return $@"<!DOCTYPE html>
<html lang='uk'>
<head>
  <meta charset='utf-8' />
  <meta name='viewport' content='width=device-width, initial-scale=1' />
  <title>{WebUtility.HtmlEncode(title)}</title>
</head>
<body style='margin:0;padding:0;background:#F7F9F8;'>
  <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background:#F7F9F8;padding:24px 0;'>
    <tr>
      <td align='center'>
        <table role='presentation' width='600' cellpadding='0' cellspacing='0' style='max-width:600px;width:100%;background:#FFFFFF;border-radius:12px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;'>
          <tr>
            <td style='background:#0E6E4E;'>
              <img src='{bannerUrl}' width='600' alt='Лілейка' style='display:block;width:100%;max-width:600px;height:auto;border:0;' />
            </td>
          </tr>
          <tr>
            <td style='padding:32px 32px 8px;'>
              <h1 style='margin:0 0 16px;font-size:22px;line-height:1.3;color:#0E6E4E;'>{WebUtility.HtmlEncode(title)}</h1>
              {text}
              <table role='presentation' cellpadding='0' cellspacing='0' style='margin:24px 0;'>
                <tr>
                  <td style='background:#0E6E4E;border-radius:8px;'>
                    <a href='{encodedUrl}' style='display:inline-block;padding:12px 28px;color:#FBF8F1;font-size:16px;font-weight:bold;text-decoration:none;'>{WebUtility.HtmlEncode(buttonText)}</a>
                  </td>
                </tr>
              </table>
              <p style='margin:0 0 8px;font-size:14px;line-height:1.5;color:#5B6763;'>Якщо кнопка не працює, скопіюйте це посилання в браузер:</p>
              <p style='margin:0 0 24px;font-size:13px;line-height:1.5;word-break:break-all;'><a href='{encodedUrl}' style='color:#0E6E4E;'>{encodedUrl}</a></p>
              <p style='margin:0 0 24px;font-size:14px;line-height:1.5;color:#5B6763;'>Не бачите наших листів? Перевірте теку «Спам» і додайте адресу відправника до контактів.</p>
            </td>
          </tr>
          <tr>
            <td style='padding:16px 32px 28px;border-top:1px solid #E3E8E6;'>
              <p style='margin:0;font-size:12px;line-height:1.5;color:#8A9490;'>{WebUtility.HtmlEncode(footer)}</p>
              <p style='margin:8px 0 0;font-size:12px;line-height:1.5;color:#8A9490;'>Лілейка · система обліку куреня. Лист надіслано автоматично.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }
}
