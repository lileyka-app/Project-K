using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>Opens a kurin's <see cref="ScoreBook"/>: the one place that knows what a read needs.</summary>
public sealed class ScoreBookReader
{
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;
    private readonly IScoreFactSource _facts;
    private readonly TimeProvider _time;

    public ScoreBookReader(
        IScoreUnitOfWork score,
        IUnitOfWork unitOfWork,
        IMembershipDirectory memberships,
        IMemberDirectory members,
        IScoreFactSource facts,
        TimeProvider time)
    {
        _score = score;
        _unitOfWork = unitOfWork;
        _memberships = memberships;
        _members = members;
        _facts = facts;
        _time = time;
    }

    public Task<ScoreBook> OpenAsync(Guid kurinKey, CancellationToken cancellationToken) =>
        ScoreBook.OpenAsync(kurinKey, _score, _unitOfWork, _memberships, _members, _facts, _time, cancellationToken);

    public async Task<ScoreAttendanceBook> OpenAttendanceAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var rates = await _score.ScoreAttendanceRates.GetForKurinAsync(kurinKey, cancellationToken);
        var attendances = await _score.ScoreAttendances.GetForKurinAsync(kurinKey, cancellationToken);
        return new ScoreAttendanceBook(rates, attendances);
    }
}
