using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Feedback.UploadScreenshot;

/// <summary>
/// One picture for a problem report, stored before the report itself exists so the editor can
/// show it inline. Returns the public URL the report will embed.
/// </summary>
public sealed record UploadFeedbackScreenshotCommand(Stream Content, string FileName) : IRequest<ServiceResult<string>>;
