using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;

namespace ProjectK.Infrastructure.Services.EmailService;

public class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MOCK EMAIL] To: {To}, Subject: {Subject}, Body: {Body}", to, subject, body);
        return Task.CompletedTask;
    }

    public Task SendInvitationEmailAsync(string to, string token, CancellationToken cancellationToken = default)
    {
        var body = $"Ваш токен запрошення: {token}";
        return SendEmailAsync(to, "Лілейка · запрошення до системи", body, cancellationToken);
    }

    public Task SendPasswordResetEmailAsync(string to, string token, CancellationToken cancellationToken = default)
    {
        var body = $"Ваш токен відновлення пароля: {token}";
        return SendEmailAsync(to, "Лілейка · відновлення пароля", body, cancellationToken);
    }

    public Task SendEmailChangeConfirmationEmailAsync(string to, string currentEmail, string confirmationUrl, CancellationToken cancellationToken = default)
    {
        var body = $"Підтвердження зміни пошти з {currentEmail}: {confirmationUrl}";
        return SendEmailAsync(to, "Лілейка · підтвердження зміни пошти", body, cancellationToken);
    }

    public Task SendWaitlistSubmittedEmailAsync(string to, string applicantName, string? claimedKurin, CancellationToken cancellationToken = default)
    {
        var body = $"Нова заявка: {applicantName}, {claimedKurin ?? "курінь не вказано"}";
        return SendEmailAsync(to, "Лілейка · нова заявка на розгляд", body, cancellationToken);
    }

    public Task SendKurinBranchRequestEmailAsync(string to, string kurin, string wanted, CancellationToken cancellationToken = default)
    {
        var body = $"Запит на зміну гілок: {kurin} → {wanted}";
        return SendEmailAsync(to, "Лілейка · запит на зміну гілок куреня", body, cancellationToken);
    }
}
