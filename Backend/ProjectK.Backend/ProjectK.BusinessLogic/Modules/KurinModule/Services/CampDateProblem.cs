using ProjectK.Common.Entities.KurinModule.Planning;
using ProjectK.Optimization.Scheduling;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

public class CampDateProblem : BaseCampDateProblem
{
    private readonly List<PlanningParticipant> _participants;

    public CampDateProblem(DateTime searchStart, DateTime searchEnd, int durationDays, List<PlanningParticipant> participants)
        : base(searchStart, searchEnd, durationDays)
    {
        _participants = participants;
    }

    public override double CalculateFitness(double[] position)
    {
        DateTime proposedStart = PositionToDate(position);
        DateTime proposedEnd = proposedStart.AddDays(DurationDays);

        double totalPenalty = 0;

        foreach (var participant in _participants)
        {
            bool isBusy = false;
            foreach (var range in participant.BusyRanges)
            {
                // Логіка перетину двох відрізків часу
                if (proposedStart < range.End && range.Start < proposedEnd)
                {
                    isBusy = true;
                    break;
                }
            }

            if (isBusy)
            {
                // Штраф = Базова вартість (100) * Важливість людини
                totalPenalty += 100.0 * participant.RoleWeight;
            }
        }

        return totalPenalty;
    }
}
