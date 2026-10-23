import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from '@openng/optimus-ui/api';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { TileBoardComponent } from '../../../../shared/tile-board/tile-board';
import { TILE_BOARD_KEYS } from '../../../../shared/tile-board/tile-board.models';
import { TileDefDirective } from '../../../../shared/tile-board/tile-def.directive';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinScopeOption } from '../../../kurinModule/models/kurin-scope-option.model';
import { MemberDto } from '../../../kurinModule/models/member.dto';
import { ROLE_DISPLAY_NAMES } from '../../../kurinModule/models/role-display-name.model';
import { LeadershipRole } from '../../../kurinModule/models/enums/leadership-role.enum';
import { MemberService } from '../../../kurinModule/services/member-service/member.service';
import { MyDuesTileComponent } from '../../components/my-dues-tile/my-dues-tile';
import { MyDutiesTileComponent, dutyRowKey } from '../../components/my-duties-tile/my-duties-tile';
import { MyKurinsTileComponent } from '../../components/my-kurins-tile/my-kurins-tile';
import { MyProbeTileComponent } from '../../components/my-probe-tile/my-probe-tile';
import { MyProfileTileComponent } from '../../components/my-profile-tile/my-profile-tile';
import { MyScoreTileComponent } from '../../components/my-score-tile/my-score-tile';
import { MySkillsTileComponent } from '../../components/my-skills-tile/my-skills-tile';
import { MyTasksTileComponent, TaskStatusChange } from '../../components/my-tasks-tile/my-tasks-tile';
import { EventResponseChange, UpcomingEventsTileComponent, eventRowKey } from '../../components/upcoming-events-tile/upcoming-events-tile';
import { MyDuesDto, MyDutyDto, MyEventDto, MyGroupDto, MyGrowthDto, MyScoreDto, MyTaskDto } from '../../models/me.dto';
import { MeService } from '../../services/me.service';
import { AgendaService } from '../../../kurinModule/services/agenda-service/agenda.service';
import { ScoreService } from '../../../scoreModule/services/score-service/score.service';
import { greeting, todayLabel } from '../../functions/greeting.function';
import { kurinName } from '../../../../shared/functions/kurin-label.function';

/**
 * The first screen of a signed-in person: who they are and where they stand, as tiles they may
 * arrange or put away. Each tile is fed by the page and knows nothing of where its data came from.
 */
@Component({
  selector: 'app-dashboard',
  imports: [
    TileBoardComponent, TileDefDirective,
    MyProfileTileComponent, MyKurinsTileComponent, UpcomingEventsTileComponent, MyTasksTileComponent,
    MyProbeTileComponent, MySkillsTileComponent, MyDuesTileComponent, MyScoreTileComponent, MyDutiesTileComponent
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly members = inject(MemberService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  private readonly me = inject(MeService);
  private readonly agenda = inject(AgendaService);
  private readonly scoreService = inject(ScoreService);

  readonly boardKey = TILE_BOARD_KEYS.dashboard;

  readonly member = signal<MemberDto | null>(null);
  readonly groups = signal<MyGroupDto[]>([]);
  readonly kurins = signal<KurinScopeOption[]>([]);
  readonly memberLoading = signal(true);
  readonly kurinsLoading = signal(true);
  readonly openingKey = signal<string | null>(null);

  readonly events = signal<MyEventDto[]>([]);
  readonly eventsLoading = signal(true);
  readonly eventsFailed = signal(false);
  readonly respondingTo = signal<string | null>(null);

  readonly tasks = signal<MyTaskDto[]>([]);
  readonly tasksLoading = signal(true);
  readonly tasksFailed = signal(false);
  readonly movingTask = signal<string | null>(null);

  /* Tiles that are not for everyone: each appears only once its reply says there is something to show. */
  readonly growth = signal<MyGrowthDto | null>(null);
  readonly growthLoading = signal(true);
  readonly growthFailed = signal(false);
  readonly dues = signal<MyDuesDto[]>([]);
  readonly duesLoading = signal(true);
  readonly duesFailed = signal(false);
  readonly scores = signal<MyScoreDto[]>([]);
  readonly scoresLoading = signal(true);
  readonly scoresFailed = signal(false);
  readonly duties = signal<MyDutyDto[]>([]);
  readonly dutiesLoading = signal(true);
  readonly dutiesFailed = signal(false);
  readonly dutySaving = signal<string | null>(null);

  /** Проба and вмілості belong to a youth of УПЮ; the reply says so, and until it comes nothing is shown. */
  readonly hasYouthProgram = computed(() => this.growth()?.hasYouthProgram ?? false);

  /** A row names its kurin only when there is more than one to tell apart. */
  readonly namesKurin = computed(() => this.kurins().length > 1);

  readonly now = new Date();
  readonly greeting = greeting(this.now);
  readonly today = todayLabel(this.now);

  readonly memberKey = computed(() => this.auth.getAuthStateValue()?.memberKey ?? null);
  readonly currentKurinKey = computed(() => this.auth.getAuthStateValue()?.kurinKey ?? null);

  readonly firstName = computed(() => this.member()?.firstName ?? '');

  /** The offices the person holds in the kurin they act in now — the card's own list, read the same way. */
  readonly currentOffices = computed(() =>
    (this.member()?.leadershipHistories ?? [])
      .filter(history => !history.endDate)
      .map(history => {
        const role = ROLE_DISPLAY_NAMES[history.role as LeadershipRole] ?? history.role;
        return history.groupName ? `${role} · ${history.groupName}` : role;
      })
  );

  ngOnInit(): void {
    const memberKey = this.memberKey();
    if (!memberKey) {
      this.memberLoading.set(false);
      this.kurinsLoading.set(false);
      return;
    }

    this.members.getByKey(memberKey).subscribe({
      next: member => { this.member.set(member); this.memberLoading.set(false); },
      error: () => this.memberLoading.set(false)
    });
    this.me.getGroups().subscribe({
      next: groups => this.groups.set(groups),
      error: () => this.groups.set([])
    });
    this.auth.getKurinScopeOptions().subscribe({
      next: kurins => { this.kurins.set(kurins); this.kurinsLoading.set(false); },
      error: () => this.kurinsLoading.set(false)
    });
    this.loadEvents();
    this.loadTasks();
    this.loadGrowth();
    this.loadDues();
    this.loadScores();
    this.loadDuties();
  }

  private loadDuties(): void {
    this.me.getDuties().subscribe({
      next: duties => { this.duties.set(duties); this.dutiesLoading.set(false); this.dutiesFailed.set(false); },
      error: () => { this.dutiesLoading.set(false); this.dutiesFailed.set(true); }
    });
  }

  private loadGrowth(): void {
    this.me.getGrowth().subscribe({
      next: growth => { this.growth.set(growth); this.growthLoading.set(false); this.growthFailed.set(false); },
      error: () => { this.growthLoading.set(false); this.growthFailed.set(true); }
    });
  }

  private loadDues(): void {
    this.me.getDues().subscribe({
      next: dues => { this.dues.set(dues); this.duesLoading.set(false); this.duesFailed.set(false); },
      error: () => { this.duesLoading.set(false); this.duesFailed.set(true); }
    });
  }

  private loadScores(): void {
    this.me.getScore().subscribe({
      next: scores => { this.scores.set(scores); this.scoresLoading.set(false); this.scoresFailed.set(false); },
      error: () => { this.scoresLoading.set(false); this.scoresFailed.set(true); }
    });
  }

  private loadEvents(): void {
    this.me.getEvents().subscribe({
      next: events => { this.events.set(events); this.eventsLoading.set(false); this.eventsFailed.set(false); },
      error: () => { this.eventsLoading.set(false); this.eventsFailed.set(true); }
    });
  }

  private loadTasks(): void {
    this.me.getTasks().subscribe({
      next: tasks => { this.tasks.set(tasks); this.tasksLoading.set(false); this.tasksFailed.set(false); },
      error: () => { this.tasksLoading.set(false); this.tasksFailed.set(true); }
    });
  }

  /** The answer lands on the clicked row at once; the other occurrences of a series keep their own. */
  respond({ event, status }: EventResponseChange): void {
    if (event.myResponse === status || this.respondingTo()) {
      return;
    }
    const rowKey = eventRowKey(event);
    this.respondingTo.set(rowKey);
    this.me.setEventResponse(event.agendaItemKey, status, event.isRecurring ? event.startUtc : null).subscribe({
      next: () => {
        this.respondingTo.set(null);
        this.events.set(this.events().map(e => eventRowKey(e) === rowKey ? { ...e, myResponse: status } : e));
      },
      error: (error: unknown) => {
        this.respondingTo.set(null);
        this.messages.add({ severity: 'error', summary: 'Не вдалося відповісти', detail: failureDetail(error, 'Спробуй ще раз.') });
      }
    });
  }

  /** «Сходин не було» from the tile: said through the sheet's own endpoint, and the row leaves at once. */
  markNotHeld(duty: MyDutyDto): void {
    if (this.dutySaving() || !duty.agendaItemKey || !duty.occurrenceStartUtc) {
      return;
    }
    const rowKey = dutyRowKey(duty);
    this.dutySaving.set(rowKey);
    this.scoreService.setNotHeld(duty.kurin.kurinKey, duty.agendaItemKey, duty.occurrenceStartUtc, true).subscribe({
      next: () => {
        this.dutySaving.set(null);
        this.duties.set(this.duties().filter(d => dutyRowKey(d) !== rowKey));
        this.messages.add({ severity: 'success', summary: 'Сходин не було', detail: `${duty.title ?? 'Подію'} прибрано зі справ. Повернути можна на аркуші присутності.` });
      },
      error: (error: unknown) => {
        this.dutySaving.set(null);
        const detail = (error as { status?: number } | null)?.status === 409
          ? 'Хтось уже відмічений на цих сходинах — отже, вони відбулися.'
          : failureDetail(error, 'Спробуй ще раз.');
        this.messages.add({ severity: 'error', summary: 'Не вдалося зберегти', detail });
      }
    });
  }

  /** Moved through the board's own endpoint: it answers for the kurin the token acts in, which is the only place the tile offers it. */
  moveTask({ task, status }: TaskStatusChange): void {
    if (this.movingTask()) {
      return;
    }
    this.movingTask.set(task.agendaItemKey);
    this.agenda.changeStatus(task.agendaItemKey, status).subscribe({
      next: () => {
        this.movingTask.set(null);
        this.tasks.set(status === 'Done'
          ? this.tasks().filter(t => t.agendaItemKey !== task.agendaItemKey)
          : this.tasks().map(t => t.agendaItemKey === task.agendaItemKey ? { ...t, status } : t));
      },
      error: (error: unknown) => {
        this.movingTask.set(null);
        this.messages.add({ severity: 'error', summary: 'Не вдалося змінити', detail: failureDetail(error, 'Спробуй ще раз.') });
      }
    });
  }

  /** A kurin is opened in the scope it belongs to: the one acted in already, or after a switch of token. */
  openKurin(option: KurinScopeOption): void {
    this.openIn(option.kurinKey, option.kurinNumber, option.kurinKey, ['/kurin']);
  }

  openGroup(group: MyGroupDto): void {
    this.openIn(group.kurin.kurinKey, group.kurin.kurinNumber, group.groupKey, ['/group', group.groupKey]);
  }

  private openIn(kurinKey: string, kurinNumber: number | null, rowKey: string, path: string[]): void {
    if (kurinKey === this.currentKurinKey()) {
      this.router.navigate(path);
      return;
    }
    if (this.openingKey()) {
      return;
    }
    this.openingKey.set(rowKey);
    this.auth.setKurinScope(kurinKey).subscribe({
      next: () => {
        this.openingKey.set(null);
        this.router.navigate(path);
      },
      error: (error: unknown) => {
        this.openingKey.set(null);
        this.messages.add({
          severity: 'error',
          summary: 'Не вдалося перейти',
          detail: failureDetail(error, `${kurinName(kurinNumber)} лишився недосяжним. Спробуй ще раз.`)
        });
      }
    });
  }
}
