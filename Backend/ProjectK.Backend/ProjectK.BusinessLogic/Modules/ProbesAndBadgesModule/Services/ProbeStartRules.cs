using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>What signing or moving a probe answers when <see cref="IMemberProbeLine.CanStartAsync"/> says no.</summary>
public static class ProbeStartRules
{
    public static ServiceResult<T>? Of<T>(ProbeStart start) => start switch
    {
        ProbeStart.NotInLine => ServiceResult<T>.Failure(
            ResultType.Conflict, ProbesAndBadgesErrors.ProbeNotInLine, "This probe is not one the member walks: it belongs to another edition of the programme."),
        ProbeStart.WaitsForPrevious => ServiceResult<T>.Failure(
            ResultType.Conflict, ProbesAndBadgesErrors.ProbeWaitsForPrevious, "This probe opens once the one before it is closed."),
        _ => null
    };
}
