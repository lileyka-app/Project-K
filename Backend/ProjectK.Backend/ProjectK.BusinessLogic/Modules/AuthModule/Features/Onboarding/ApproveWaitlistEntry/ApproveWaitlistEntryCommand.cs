using System;
using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ApproveWaitlistEntry;

/// <param name="KraiCode">
/// The край the admin settled on, overriding the applicant's pick. Without it the entry's own край
/// stands, and without that the instance's default.
/// </param>
public record ApproveWaitlistEntryCommand(Guid WaitlistEntryKey, string? KraiCode = null) : IRequest<ServiceResult<Guid>>;
