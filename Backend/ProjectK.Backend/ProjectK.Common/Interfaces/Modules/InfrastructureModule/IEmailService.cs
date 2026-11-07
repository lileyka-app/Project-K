using System.Threading;
using System.Threading.Tasks;

namespace ProjectK.Common.Interfaces.Modules.InfrastructureModule;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    Task SendInvitationEmailAsync(string to, string token, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string to, string token, CancellationToken cancellationToken = default);
    Task SendEmailChangeConfirmationEmailAsync(string to, string currentEmail, string confirmationUrl, CancellationToken cancellationToken = default);
    /// <param name="claimedKurin">The kurin as the letter names it: «курінь 47», «5-й курінь УСП · 20-й курінь УПС».</param>
    Task SendWaitlistSubmittedEmailAsync(string to, string applicantName, string? claimedKurin, CancellationToken cancellationToken = default);

    /// <summary>Tells an administrator a senior kurin asks to change its гілки.</summary>
    /// <param name="kurin">The kurin as it stands: «Орден Хрестоносців» · 5-й курінь УСП.</param>
    /// <param name="wanted">What it asks for: «5-й курінь УСП · 20-й курінь УПС».</param>
    Task SendKurinBranchRequestEmailAsync(string to, string kurin, string wanted, CancellationToken cancellationToken = default);
}
