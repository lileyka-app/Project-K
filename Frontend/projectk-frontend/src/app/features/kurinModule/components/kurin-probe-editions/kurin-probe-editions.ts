import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SelectModule } from '@openng/optimus-ui/select';
import { forkJoin, map, Observable, of, tap } from 'rxjs';
import {
  GroupMemberStandingDto,
  GroupProbeEditionDto,
  KurinProbeEditionsDto,
  probeEditionLabel
} from '../../models/group-probe-edition.dto';
import { GroupService } from '../../services/group-service/group.service';

/** У `p-select` значення null читається як «нічого не вибрано», тож редакція без назви ходить як ''. */
const UNNAMED = '';

/** Чим юнак піде після збереження і чому саме так. */
export interface MemberOutcome {
  member: GroupMemberStandingDto;
  edition: string | null;
  /** Чому не просто пробою гуртка, або що станеться з розпочатою пробою; null — нічого особливого. */
  note: string | null;
  kind: 'follows' | 'held' | 'abandons' | 'continues';
}

/**
 * Проба кожного гуртка — частина налаштувань куреня, а не сторінки гуртка. Розділ видно,
 * лише коли каталог пропонує краю куреня більше однієї редакції: інакше обирати нема з чого.
 * Зберігається спільною кнопкою сторінки налаштувань, як і решта розділів.
 */
@Component({
  selector: 'app-kurin-probe-editions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, SelectModule],
  templateUrl: './kurin-probe-editions.html',
  styleUrl: './kurin-probe-editions.css'
})
export class KurinProbeEditionsComponent implements OnInit {
  private readonly groups = inject(GroupService);

  readonly kurinKey = input.required<string>();

  protected readonly state = signal<KurinProbeEditionsDto | null>(null);
  protected readonly failed = signal(false);
  /** Вибір на сторінці, ще не збережений: гурток → редакція ('' — звичайна). */
  protected readonly chosen = signal<Record<string, string>>({});

  /** Чи є з чого обирати; без цього розділ не показується зовсім. */
  readonly offersChoice = computed(() => (this.state()?.editions.length ?? 0) > 1);

  protected readonly options = computed(() =>
    (this.state()?.editions ?? []).map(edition => ({
      value: edition.id ?? UNNAMED,
      label: this.editionLabel(edition.id) + (edition.isTrial ? ' · апробація' : '')
    })));

  protected editionLabel(edition: string | null): string {
    return probeEditionLabel(this.state()?.editions ?? [], edition);
  }

  ngOnInit(): void {
    this.load().subscribe();
  }

  protected selected(group: GroupProbeEditionDto): string {
    return this.chosen()[group.groupKey] ?? group.edition ?? UNNAMED;
  }

  protected isChanged(group: GroupProbeEditionDto): boolean {
    return this.selected(group) !== (group.edition ?? UNNAMED);
  }

  protected choose(group: GroupProbeEditionDto, value: string): void {
    this.chosen.update(current => ({ ...current, [group.groupKey]: value }));
  }

  protected probesOf(group: GroupProbeEditionDto): string[] {
    return this.state()?.editions.find(e => (e.id ?? UNNAMED) === this.selected(group))?.probeTitles ?? [];
  }

  protected outcomes(group: GroupProbeEditionDto): MemberOutcome[] {
    return group.members.map(member => memberOutcome(member, this.selected(group) || null));
  }

  protected count(group: GroupProbeEditionDto, kind: MemberOutcome['kind']): number {
    return this.outcomes(group).filter(outcome => outcome.kind === kind).length;
  }

  /** Гуртки, які перемкнуться після «Зберегти». */
  pendingCount(): number {
    return (this.state()?.groups ?? []).filter(group => group.canChange && this.isChanged(group)).length;
  }

  /** Зберігає змінені гуртки; сторінка налаштувань каже, як минуло збереження. */
  save(): Observable<void> {
    const changed = (this.state()?.groups ?? []).filter(group => group.canChange && this.isChanged(group));
    if (changed.length === 0) {
      return of(undefined);
    }
    return forkJoin(changed.map(group => this.groups.setProbeEdition(group.groupKey, this.selected(group) || null))).pipe(
      map(() => undefined),
      tap(() => this.load().subscribe())
    );
  }

  private load(): Observable<void> {
    return this.groups.getKurinProbeEditions(this.kurinKey()).pipe(
      tap({
        next: state => {
          this.state.set(state);
          this.chosen.set({});
          this.failed.set(false);
        },
        error: () => this.failed.set(true)
      }),
      map(() => undefined)
    );
  }
}

/** Чим юнак піде, якщо гурток іде пробою `groupEdition`. */
export function memberOutcome(member: GroupMemberStandingDto, groupEdition: string | null): MemberOutcome {
  if (member.hasStarted && member.isHeld) {
    return {
      member,
      edition: member.edition,
      kind: member.edition === groupEdition ? 'follows' : 'held',
      note: member.edition === groupEdition ? null : `закрив «${member.probeTitle}», продовжує свою пробу`
    };
  }
  if (member.hasStarted && member.edition !== groupEdition) {
    return { member, edition: groupEdition, kind: 'abandons', note: `не продовжує розпочату «${member.probeTitle}»; її підписи — у журналі дій` };
  }
  if (member.hasStarted) {
    return { member, edition: groupEdition, kind: 'continues', note: `продовжує «${member.probeTitle}»` };
  }
  return { member, edition: groupEdition, kind: 'follows', note: null };
}
