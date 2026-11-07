using System;
using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.SubmitWaitlistRegistration;

/// <param name="KraiCode">
/// The край picked on the form. Optional: without it the kurin opens in the instance's default край.
/// </param>
/// <param name="UspNumber">
/// The senior kurin's number in УСП. With <paramref name="UpsNumber"/> or alone it makes this a senior
/// kurin's application, and <paramref name="ClaimedKurinNameOrNumber"/> is left out.
/// </param>
/// <param name="UpsNumber">The senior kurin's number in УПС; see <paramref name="UspNumber"/>.</param>
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
    string? KraiCode = null,
    int? UspNumber = null,
    int? UpsNumber = null) : IRequest<ServiceResult<Guid>>;
