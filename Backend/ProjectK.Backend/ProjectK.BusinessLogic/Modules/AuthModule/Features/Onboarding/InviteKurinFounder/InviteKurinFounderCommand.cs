using MediatR;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.InviteKurinFounder;

/// <summary>
/// An admin opens a kurin and invites the person who will be its Зв'язковий. No password passes
/// through the admin: the person gets the same letter an approved kurin-leader application sends,
/// chooses their own password on activation, and is seated then.
/// </summary>
public sealed record InviteKurinFounderCommand(
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    int? KurinNumber,
    string? KraiCode) : IRequest<ServiceResult<KurinFounderInvitation>>;
