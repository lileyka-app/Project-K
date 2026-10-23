import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import {
  ActionSheetController,
  IonAccordion,
  IonAccordionGroup,
  IonBackButton,
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonNote,
  IonProgressBar,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { checkmarkCircle, ellipseOutline } from 'ionicons/icons';
import { apiErrorText } from '../../core/api';
import { FAILED_TEXT } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import {
  ProbePointView,
  hasYouthProgram,
  isProbeClosed,
  memberBranch,
  probeSections,
  shortDate,
  signerLabel,
  utcDate,
} from './kurin.labels';
import { GroupedProbeDto, MemberDto, MembershipDto, ProbeProgressDto } from './kurin.models';
import { KurinApi } from './kurin.service';
import { MemberAbilities, NO_ABILITIES } from './abilities';

const CONFLICT = { Conflict: 'Стан проби вже змінено. Дані оновлено.' };

/**
 * One probe of a member (web member-probe-page): sections as an accordion with every point and who
 * signed it when. Whoever the kurin lets sign does so and, once all are signed, closes the probe;
 * taking a signature back is a right of its own. A senior's probe is their archive: read-only. The probe comes as
 * `?id=probe-1|probe-2` (the route has no :probeId).
 */
@Component({
  selector: 'app-probe',
  imports: [
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonCard,
    IonCardContent,
    IonAccordionGroup,
    IonAccordion,
    IonList,
    IonItem,
    IonLabel,
    IonNote,
    IonIcon,
    IonProgressBar,
    IonSkeletonText,
    IonSpinner,
  ],
  styles: `
    .summary h2 {
      margin: 0;
      font-size: 20px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .summary p {
      margin: 4px 0 0;
    }
    ion-progress-bar {
      margin: 10px 0 4px;
      height: 6px;
      border-radius: 3px;
    }
    .close p {
      margin: 0 0 10px;
    }
    .close strong {
      color: var(--lk-ink);
    }
    .signed {
      color: var(--lk-primary);
    }
    .unsigned {
      color: var(--lk-faint);
    }
    ion-item ion-icon[slot='start'] {
      margin-inline-end: 12px;
      font-size: 22px;
    }
    .point-title {
      color: var(--lk-ink);
      font-weight: 600;
    }
    ion-accordion-group {
      margin-bottom: 24px;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/kurin" text="Назад" /></ion-buttons>
        <ion-title>{{ probe()?.title ?? 'Проба' }}</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>

      @if (loading()) {
        <ion-card><ion-card-content><ion-skeleton-text [animated]="true" style="height: 64px" /></ion-card-content></ion-card>
      } @else if (!probe()) {
        <ion-list [inset]="true"><ion-item><ion-label class="ion-text-wrap">{{ failedText }}</ion-label></ion-item></ion-list>
      } @else {
        <ion-card class="summary" data-testid="probe-summary">
          <ion-card-content>
            <h2>{{ probe()!.title }}</h2>
            <p>{{ memberName() }}</p>
            @if (archive()) { <p>Архів юнацтва · тільки перегляд</p> }
            @if (total()) {
              <ion-progress-bar [value]="signedCount() / total()" />
              <p>Підписано {{ signedCount() }} з {{ total() }} · {{ percent() }}%</p>
            }
            @if (closedAt(); as date) { <p>Пробу закрито {{ date }}</p> }
            @if (partial()) { <p>Частина даних про підписи недоступна.</p> }
          </ion-card-content>
        </ion-card>

        @if (canClose()) {
          <ion-card class="close" data-testid="close-probe">
            <ion-card-content>
              <p><strong>Усі точки підписані.</strong> Можна здати і закрити пробу.</p>
              <ion-button expand="block" [disabled]="busy() !== null" (click)="confirmClose()">Здати і закрити пробу</ion-button>
            </ion-card-content>
          </ion-card>
        }

        <ion-accordion-group expand="inset" [multiple]="true" [value]="openSections()" (ionChange)="openSections.set($any($event).detail.value ?? [])">
          @for (section of sections(); track section.sectionId) {
            <ion-accordion [value]="section.sectionId" data-testid="probe-section">
              <ion-item slot="header">
                <ion-label class="ion-text-wrap">{{ section.code }} · {{ section.title }}</ion-label>
                <ion-note slot="end">{{ signedIn(section.points) }}/{{ section.points.length }}</ion-note>
              </ion-item>
              <div slot="content">
                @for (point of section.points; track point.pointId) {
                  <ion-item data-testid="probe-point">
                    <ion-icon
                      slot="start"
                      [name]="point.isSigned ? 'checkmark-circle' : 'ellipse-outline'"
                      [class.signed]="point.isSigned"
                      [class.unsigned]="!point.isSigned"
                      [attr.aria-label]="point.isSigned ? 'Підписано' : 'Не підписано'"
                    />
                    <ion-label class="ion-text-wrap">
                      <span class="point-title">{{ point.title }}</span>
                      @if (point.isSigned) {
                        <p>{{ signer(point) }} · {{ signedAt(point) }}</p>
                      }
                    </ion-label>
                    @if (point.isSigned ? canUnsign() : canSign()) {
                      <ion-button
                        slot="end"
                        fill="clear"
                        size="default"
                        [color]="point.isSigned ? 'medium' : 'primary'"
                        [disabled]="busy() !== null"
                        (click)="point.isSigned ? confirmUnsign(point) : sign(point, true)"
                      >
                        @if (busy() === point.pointId) { <ion-spinner name="crescent" /> }
                        @else { {{ point.isSigned ? 'Скасувати' : 'Підписати' }} }
                      </ion-button>
                    }
                  </ion-item>
                } @empty {
                  <ion-item><ion-label>У цьому розділі поки немає точок.</ion-label></ion-item>
                }
              </div>
            </ion-accordion>
          }
        </ion-accordion-group>
      }
    </ion-content>
  `,
})
export class ProbePage implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);
  private readonly route = inject(ActivatedRoute);

  private readonly memberKey = this.route.snapshot.paramMap.get('memberKey') ?? '';
  private readonly probeId = this.route.snapshot.queryParamMap.get('id') ?? 'probe-1';

  protected readonly failedText = FAILED_TEXT;
  protected readonly loading = signal(true);
  protected readonly probe = signal<GroupedProbeDto | null>(null);
  protected readonly progress = signal<ProbeProgressDto | null>(null);
  protected readonly partial = signal(false);
  private readonly member = signal<MemberDto | null>(null);
  private readonly memberships = signal<MembershipDto[]>([]);
  /** What the kurin's policy lets this person do to the member: sign, and separately unsign. */
  private readonly abilities = signal<MemberAbilities>(NO_ABILITIES);
  protected readonly busy = signal<string | null>(null);

  protected readonly sections = computed(() => probeSections(this.probe(), this.progress()));
  private readonly points = computed(() => this.sections().flatMap((s) => s.points));
  protected readonly total = computed(() => this.points().length);
  protected readonly signedCount = computed(() => this.points().filter((p) => p.isSigned).length);
  protected readonly percent = computed(() => (this.total() ? Math.round((this.signedCount() / this.total()) * 100) : 0));
  /** The first section with something left to sign starts open; then the accordion is the person's. */
  protected readonly openSections = signal<string[] | null>(null);
  protected readonly memberName = computed(() => {
    const m = this.member();
    return m ? `Учасник: ${m.firstName} ${m.lastName}` : '';
  });
  /** A senior's probe is a page of their record, not training: seen, not signed. */
  protected readonly archive = computed(() => {
    const m = this.member();
    return !!m && !hasYouthProgram(memberBranch(m, this.memberships()));
  });
  protected readonly canSign = computed(() => !this.archive() && this.abilities().probes.sign);
  protected readonly canUnsign = computed(() => !this.archive() && this.abilities().probes.unsign);
  protected readonly closed = computed(() => isProbeClosed(this.progress()));
  protected readonly canClose = computed(
    () =>
      this.canSign() &&
      ['probe-1', 'probe-2'].includes(this.probeId) &&
      !this.closed() &&
      this.total() > 0 &&
      this.signedCount() === this.total(),
  );
  protected readonly closedAt = computed(() => {
    const p = this.progress();
    if (!this.closed() || !p) return null;
    const at = p.completedAtUtc ?? p.verifiedAtUtc;
    return at ? shortDate(utcDate(at)) : null;
  });

  constructor() {
    addIcons({ checkmarkCircle, ellipseOutline });
  }

  ngOnInit(): void {
    void this.load();
  }

  protected async refresh(event: Event): Promise<void> {
    await this.load();
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected signer(point: ProbePointView): string {
    return signerLabel(point);
  }

  protected signedAt(point: ProbePointView): string {
    return point.signedAtUtc ? shortDate(utcDate(point.signedAtUtc)) : '—';
  }

  protected signedIn(points: ProbePointView[]): number {
    return points.filter((p) => p.isSigned).length;
  }

  protected async sign(point: ProbePointView, sign: boolean): Promise<void> {
    if (this.busy()) return;
    this.busy.set(point.pointId);
    try {
      this.progress.set(await this.data.signPoint(this.memberKey, this.probeId, point.pointId, sign));
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося оновити підпис точки. Спробуй ще раз.', CONFLICT), 'danger');
      await this.load();
    } finally {
      this.busy.set(null);
    }
  }

  protected async confirmUnsign(point: ProbePointView): Promise<void> {
    const sheet = await this.sheets.create({
      header: `Скасувати підпис точки «${point.title}»?`,
      buttons: [
        { text: 'Так, скасувати', role: 'destructive', data: 'unsign' },
        { text: 'Ні', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (data === 'unsign') await this.sign(point, false);
  }

  protected async confirmClose(): Promise<void> {
    const sheet = await this.sheets.create({
      header:
        this.probeId === 'probe-1'
          ? 'Закрити пробу? Після закриття першої проби відкриється друга.'
          : 'Закрити пробу? Буде зафіксовано дату закриття другої проби.',
      buttons: [
        { text: 'Здати і закрити', data: 'close' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (data !== 'close' || this.busy()) return;
    this.busy.set('close');
    try {
      this.progress.set(await this.data.closeProbe(this.memberKey, this.probeId));
      await this.toasts.show(
        this.probeId === 'probe-1' ? 'Пробу здано і закрито. Друга проба тепер доступна.' : 'Пробу здано і закрито.',
      );
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося закрити пробу. Спробуй ще раз.', CONFLICT), 'danger');
      await this.load();
    } finally {
      this.busy.set(null);
    }
  }

  private async load(): Promise<void> {
    const key = this.memberKey;
    const [member, memberships, probe, progress, abilities] = await Promise.all([
      this.data.member(key).catch(() => null),
      this.data.memberships(key).catch(() => [] as MembershipDto[]),
      this.data.groupedProbe(this.probeId).catch(() => null),
      this.data.probeProgress(key, this.probeId).catch(() => null),
      this.data.abilities(key).catch(() => NO_ABILITIES),
    ]);
    this.member.set(member);
    this.memberships.set(memberships);
    // A failed refresh keeps the probe that was shown.
    if (probe || !this.probe()) this.probe.set(probe);
    this.progress.set(progress);
    this.partial.set(!!probe && progress === null);
    this.abilities.set(abilities);
    if (this.openSections() === null) {
      const next = this.sections().find((s) => s.points.some((p) => !p.isSigned));
      this.openSections.set(next ? [next.sectionId] : []);
    }
    this.loading.set(false);
  }
}
