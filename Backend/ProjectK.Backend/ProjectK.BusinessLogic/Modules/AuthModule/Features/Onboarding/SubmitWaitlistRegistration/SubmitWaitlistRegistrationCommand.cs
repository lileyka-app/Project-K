using System;
using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.SubmitWaitlistRegistration;

/// <param name="KraiCode">
/// The край picked on the form. Optional: without it the kurin opens in the instance's default край.
/// </param>
public record SubmitWaitlistRegistrationCommand(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateTime DateOfBirth,
    string? Stanytsia,
    string? RegionOrCountry,
    bool IsKurinLeaderCandidate,
    string? ClaimedKurinNameOrNumber,
    bool IsPreparatoryKurin = false,
    string? KraiCode = null) : IRequest<ServiceResult<Guid>>;
