using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Account;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Join;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using GroupEntity = ProjectK.Common.Entities.KurinModule.Group;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;

/// <summary>
/// Reads every row, decides what it means, and — unless this is a dry run — writes it.
/// <para>
/// The same code answers both questions, which is the point: what the провід is shown before
/// confirming is produced by the run that would do the work, not by a second implementation of
/// the rules that could disagree with it.
/// </para>
/// </summary>
public sealed class ImportRosterCommandHandler : IRequestHandler<ImportRosterCommand, ServiceResult<RosterImportReport>>
{
    private readonly IUnitOfWork _kurinData;
    private readonly IMemberUnitOfWork _memberData;
    private readonly IMediator _mediator;

    public ImportRosterCommandHandler(
        IUnitOfWork kurinData,
        IMemberUnitOfWork memberData,
        IMediator mediator)
    {
        _kurinData = kurinData;
        _memberData = memberData;
        _mediator = mediator;
    }

    public async Task<ServiceResult<RosterImportReport>> Handle(
        ImportRosterCommand request,
        CancellationToken cancellationToken)
    {
        var kurin = await _kurinData.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return new ServiceResult<RosterImportReport>(ResultType.NotFound);
        }

        var rows = request.Rows.Select(row => RosterRow.From(row, request.Mapping)).ToList();

        var groups = (await _kurinData.Groups.GetAllAsync(request.KurinKey, cancellationToken)).ToList();
        var groupsByName = groups.ToDictionary(
            group => group.Name.Trim(),
            group => group.GroupKey,
            StringComparer.CurrentCultureIgnoreCase);

        var usable = rows.Where(row => row.Rejection() is null).ToList();

        var missingGroups = usable
            .Select(row => row.GroupName!.Trim())
            .Where(name => !groupsByName.ContainsKey(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var foreignKurinRows = usable
            .Where(row => row.KurinNumber is not null && row.KurinNumber != kurin.Number)
            .Select(row => row.Number)
            .ToList();

        var candidates = await _memberData.Members.FindPossibleMatchesAsync(
            usable.Where(row => row.Email is not null).Select(row => row.Email!).ToList(),
            usable.Where(row => row.PhoneNumber is not null).Select(row => row.PhoneNumber!).ToList(),
            usable.Select(row => row.LastName!).ToList(),
            cancellationToken);

        var alreadyHere = await MemberKeysInKurinAsync(request.KurinKey, cancellationToken);

        // Гуртки are opened before anyone is placed in them: a row cannot name one that will
        // exist a moment later. The whole import is inside one transaction, so a later failure
        // takes the new гуртки back out with everything else.
        if (!request.DryRun && request.CreateMissingGroups)
        {
            foreach (var name in missingGroups)
            {
                var group = new GroupEntity(name, request.KurinKey);
                _kurinData.Groups.Create(group, cancellationToken);
                groupsByName[name] = group.GroupKey;
            }

            await _kurinData.SaveChangesAsync(cancellationToken);
        }

        var results = new List<RowResult>(rows.Count);

        foreach (var row in rows)
        {
            if (row.Rejection() is { } reason)
            {
                results.Add(new RowResult(row.Number, row.DisplayName, RowOutcome.Rejected, reason));
                continue;
            }

            var groupName = row.GroupName!.Trim();
            if (!groupsByName.TryGetValue(groupName, out var groupKey))
            {
                if (!request.CreateMissingGroups)
                {
                    results.Add(new RowResult(
                        row.Number,
                        row.DisplayName,
                        RowOutcome.Rejected,
                        $"У курені немає гуртка «{groupName}»."));
                    continue;
                }

                // On a dry run the гурток does not exist yet, and saying so is the whole point of
                // the run. The row is still reported as one that would go through.
                groupKey = Guid.Empty;
            }

            var match = MatchFor(row, candidates);

            if (match is not null && alreadyHere.Contains(match.MemberKey))
            {
                results.Add(new RowResult(row.Number, row.DisplayName, RowOutcome.AlreadyHere));
                continue;
            }

            var outcome = match is null ? RowOutcome.Created : RowOutcome.Attached;

            // A row with an address gets an account: the file is how a kurin brings its people
            // in, and a person with no way to sign in is only half brought in. On a dry run this
            // is a promise, and the tally shows how many letters are about to go out.
            if (request.DryRun)
            {
                results.Add(new RowResult(row.Number, row.DisplayName, outcome, AccountInvited: row.Email is not null));
                continue;
            }

            var memberKey = match?.MemberKey;
            var failure = match is null
                ? await CreateAsync(row, request.KurinKey, groupKey, key => memberKey = key, cancellationToken)
                : await AttachAsync(match, request.KurinKey, groupKey, cancellationToken);

            if (failure is not null)
            {
                results.Add(new RowResult(row.Number, row.DisplayName, RowOutcome.Rejected, failure));
                continue;
            }

            var invitation = row.Email is not null && memberKey is { } key
                ? await InviteAsync(key, cancellationToken)
                : Invitation.NotApplicable;

            results.Add(new RowResult(row.Number, row.DisplayName, outcome, invitation.Note, invitation.Sent));
        }

        return new ServiceResult<RosterImportReport>(
            ResultType.Success,
            new RosterImportReport(results, missingGroups, foreignKurinRows, request.DryRun));
    }

    private async Task<HashSet<Guid>> MemberKeysInKurinAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var summaries = await _memberData.Members.GetSummariesByKurinKeyAsync(kurinKey, cancellationToken);
        return summaries.Select(summary => summary.MemberKey).ToHashSet();
    }

    /// <summary>
    /// Whether the system already knows this person. An address or a phone number is a match on
    /// its own; a name is not, and is trusted only together with a birthday — two juniors called
    /// Іван Петренко in one станиця is not a hypothetical.
    /// </summary>
    private static MemberIdentity? MatchFor(RosterRow row, IReadOnlyCollection<MemberIdentity> candidates)
    {
        if (row.Email is not null)
        {
            var byEmail = candidates.FirstOrDefault(
                person => string.Equals(person.Email, row.Email, StringComparison.OrdinalIgnoreCase));
            if (byEmail is not null)
            {
                return byEmail;
            }
        }

        if (row.PhoneNumber is not null)
        {
            var byPhone = candidates.FirstOrDefault(
                person => string.Equals(person.PhoneNumber, row.PhoneNumber, StringComparison.Ordinal));
            if (byPhone is not null)
            {
                return byPhone;
            }
        }

        return candidates.FirstOrDefault(person =>
            string.Equals(person.LastName, row.LastName, StringComparison.CurrentCultureIgnoreCase)
            && string.Equals(person.FirstName, row.FirstName, StringComparison.CurrentCultureIgnoreCase)
            && person.DateOfBirth == row.DateOfBirth);
    }

    private async Task<string?> CreateAsync(
        RosterRow row,
        Guid kurinKey,
        Guid groupKey,
        Action<Guid> created,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertMemberProfileCommand
        {
            MemberKey = Guid.Empty,
            KurinKey = kurinKey,
            GroupKey = groupKey == Guid.Empty ? null : groupKey,
            FirstName = row.FirstName!,
            MiddleName = row.MiddleName ?? string.Empty,
            LastName = row.LastName!,
            Email = row.Email ?? string.Empty,
            PhoneNumber = row.PhoneNumber ?? string.Empty,
            DateOfBirth = row.DateOfBirth!.Value,
            Address = row.Address,
            School = row.School,
            PlastLevelHistories = row.Levels()
        }, cancellationToken);

        if (result.Type == ResultType.Success && result.Data is not null)
        {
            created(result.Data.MemberKey);
            return null;
        }

        return "Не вдалося створити запис.";
    }

    private readonly record struct Invitation(bool Sent, string? Note)
    {
        public static Invitation NotApplicable => new(false, null);
    }

    /// <summary>
    /// Opens an account and sends the activation letter. A person who already has one is left as
    /// they are, quietly; any other refusal is noted on the row, because the import itself went
    /// through and the провід has to know who still cannot sign in.
    /// </summary>
    private async Task<Invitation> InviteAsync(Guid memberKey, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ProvisionMemberAccountCommand(memberKey), cancellationToken);
        return result.Type switch
        {
            ResultType.Success or ResultType.Created when result.Data is { Sent: false } =>
                new Invitation(false, "Акаунт відкрито, але лист не надіслано — надішліть його з картки людини."),
            ResultType.Success or ResultType.Created => new Invitation(true, null),
            ResultType.Conflict => Invitation.NotApplicable,
            _ => new Invitation(false, "Запис створено, але акаунт відкрити не вдалося.")
        };
    }

    private async Task<string?> AttachAsync(
        MemberIdentity person,
        Guid kurinKey,
        Guid groupKey,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new JoinKurinCommand(
            person.MemberKey,
            kurinKey,
            groupKey == Guid.Empty ? null : groupKey), cancellationToken);

        // Conflict means they were already taken in — by an earlier row of the same file, say.
        // Nothing went wrong and nothing needs undoing.
        return result.Type is ResultType.Success or ResultType.Created or ResultType.Conflict
            ? null
            : "Не вдалося долучити людину до куреня.";
    }
}
