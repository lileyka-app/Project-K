using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.Shared;
using GroupEntity = ProjectK.Common.Entities.KurinModule.Group;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;

/// <summary>
/// Takes a roster into a kurin. Ran twice by design: once with <paramref name="DryRun"/> to show
/// the провід what would happen, then again to do it. The rows come back from the preview rather
/// than from a second upload — the file is read once, and what is confirmed is exactly what was
/// shown.
/// </summary>
/// <param name="CreateMissingGroups">
/// Гуртки named in the file that the kurin does not have. Without this they are a reason to
/// reject the row, so nobody quietly ends up in a гурток nobody meant to open.
/// </param>
public sealed record ImportRosterCommand(
    Guid KurinKey,
    IReadOnlyList<SheetRow> Rows,
    IReadOnlyList<ColumnMapping> Mapping,
    bool CreateMissingGroups,
    bool DryRun) : IRequest<ServiceResult<RosterImportReport>>, ITransactionalRequest;

/// <summary>What the import would do, or did, with one row.</summary>
public enum RowOutcome
{
    /// <summary>A person the system has never seen. A member record is opened.</summary>
    Created,

    /// <summary>Someone already here. They are taken into the kurin, not duplicated.</summary>
    Attached,

    /// <summary>Already in this kurin. Nothing to do, and not an error.</summary>
    AlreadyHere,

    Rejected
}

/// <summary>One row's verdict, with the row number the person sees in Excel.</summary>
/// <param name="AccountInvited">
/// Whether an account was opened for the person and an activation letter sent to the row's
/// address (on a dry run: whether one would be).
/// </param>
public sealed record RowResult(
    int RowNumber,
    string Name,
    RowOutcome Outcome,
    string? Reason = null,
    bool AccountInvited = false);

/// <summary>
/// The whole picture before or after writing: what happens to each row, which гуртки would have
/// to be opened, and which rows name a different kurin than the one being imported into.
/// </summary>
public sealed record RosterImportReport(
    IReadOnlyList<RowResult> Rows,
    IReadOnlyList<string> MissingGroups,
    IReadOnlyList<int> ForeignKurinRows,
    bool DryRun)
{
    public int CreatedCount => Rows.Count(row => row.Outcome == RowOutcome.Created);

    public int AttachedCount => Rows.Count(row => row.Outcome == RowOutcome.Attached);

    public int AlreadyHereCount => Rows.Count(row => row.Outcome == RowOutcome.AlreadyHere);

    public int RejectedCount => Rows.Count(row => row.Outcome == RowOutcome.Rejected);

    public int InvitedCount => Rows.Count(row => row.AccountInvited);
}

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
    private readonly IMemberDirectory _members;
    private readonly IMemberImportWriter _people;
    private readonly ISender _sender;
    private readonly MembershipAskRouting _routing;

    public ImportRosterCommandHandler(
        IUnitOfWork kurinData,
        IMemberDirectory members,
        IMemberImportWriter people,
        ISender sender,
        MembershipAskRouting routing)
    {
        _routing = routing;
        _kurinData = kurinData;
        _members = members;
        _people = people;
        _sender = sender;
    }

    public async Task<ServiceResult<RosterImportReport>> Handle(
        ImportRosterCommand request,
        CancellationToken cancellationToken)
    {
        var kurin = await _kurinData.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return KurinErrors.NotFound<RosterImportReport>();
        }

        // A УПЮ kurin places every row in a гурток; a senior one has none, takes people into the
        // kurin itself and ignores any гурток the file names.
        var placesInGroups = RosterImportRules.PlacesInGroups(kurin.Branch);

        var rows = request.Rows.Select(row => RosterRow.From(row, request.Mapping)).ToList();

        List<GroupEntity> groups = placesInGroups
            ? [.. await _kurinData.Groups.GetAllAsync(request.KurinKey, cancellationToken)]
            : [];
        var groupsByName = groups.ToDictionary(
            group => group.Name.Trim(),
            group => group.GroupKey,
            StringComparer.CurrentCultureIgnoreCase);

        var usable = rows.Where(row => row.Rejection(placesInGroups) is null).ToList();

        List<string> missingGroups = placesInGroups
            ? [.. usable
                .Select(row => row.GroupName!.Trim())
                .Where(name => !groupsByName.ContainsKey(name))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)]
            : [];

        var foreignKurinRows = usable
            .Where(row => row.KurinNumber is not null && kurin.Numbering.All(numbering => numbering.Number != row.KurinNumber))
            .Select(row => row.Number)
            .ToList();

        var candidates = await _members.FindPossibleMatchesAsync(
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
            if (row.Rejection(placesInGroups) is { } reason)
            {
                results.Add(new RowResult(row.Number, row.DisplayName, RowOutcome.Rejected, reason));
                continue;
            }

            var groupKey = Guid.Empty;
            var groupName = row.GroupName?.Trim();
            if (placesInGroups && !groupsByName.TryGetValue(groupName!, out groupKey))
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

            // A senior kurin takes in someone already known only the way «Переходи» would: at once,
            // or not from a file at all — the person answers, or their kurin does, and nobody still in УПЮ.
            if (match is not null && !placesInGroups && await SeniorRefusalAsync(match.MemberKey, request.KurinKey, cancellationToken) is { } refusal)
            {
                results.Add(new RowResult(row.Number, row.DisplayName, RowOutcome.Rejected, refusal));
                continue;
            }

            // Nor does a УПЮ kurin take someone straight from another kurin: that kurin is asked first.
            if (match is not null && placesInGroups && await StandsElsewhereAsync(match.MemberKey, cancellationToken))
            {
                results.Add(new RowResult(row.Number, row.DisplayName, RowOutcome.Rejected, StandsElsewhere));
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

    private async Task<string?> SeniorRefusalAsync(Guid memberKey, Guid kurinKey, CancellationToken cancellationToken)
    {
        var route = await _routing.ResolveAsync(memberKey, kurinKey, cancellationToken);
        if (route.StillYouth)
        {
            return "Людина ще в УПЮ.";
        }

        return route.JoinsAtOnce ? null : "Людина вже є в Лілейці: запроси її в «Переходах».";
    }

    private const string StandsElsewhere = "Людина стоїть в іншому курені: попроси її в «Переходах».";

    private async Task<bool> StandsElsewhereAsync(Guid memberKey, CancellationToken cancellationToken)
        => (await _kurinData.Memberships.GetActiveForMemberAsync(memberKey, cancellationToken)).Count > 0;

    private async Task<HashSet<Guid>> MemberKeysInKurinAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var memberships = await _kurinData.Memberships.GetRecordsInKurinAsync(kurinKey, cancellationToken);
        return memberships.Where(membership => membership.LeftAtUtc is null).Select(membership => membership.MemberKey).ToHashSet();
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
        var memberKey = await _people.CreateAsync(new ImportedMember(
            kurinKey,
            groupKey == Guid.Empty ? null : groupKey,
            row.FirstName!,
            row.MiddleName ?? string.Empty,
            row.LastName!,
            row.Email ?? string.Empty,
            row.PhoneNumber ?? string.Empty,
            row.DateOfBirth!.Value,
            row.Address,
            row.School,
            row.Levels()), cancellationToken);

        if (memberKey is { } key)
        {
            created(key);
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
        var result = await _people.InviteAsync(memberKey, cancellationToken);
        return result.Type switch
        {
            ResultType.Success or ResultType.Created when result.Data is { Sent: false } =>
                new Invitation(false, "Акаунт відкрито, але лист не надіслано — надішли його з картки людини."),
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
        var result = await _sender.Send(
            new JoinKurinCommand(person.MemberKey, kurinKey, groupKey == Guid.Empty ? null : groupKey),
            cancellationToken);

        // Conflict means they were already taken in — say, by an earlier row of the same file.
        return result.Type is ResultType.Success or ResultType.Created or ResultType.Conflict ? null : "Не вдалося долучити людину до куреня.";
    }
}
