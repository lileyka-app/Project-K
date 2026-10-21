import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import {
  AlertController,
  IonAvatar,
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonModal,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import { AuthService } from '../../auth/auth.service';
import { Api, apiErrorText } from '../../core/api';
import { FAILED_TEXT, Loaded, settle, valueOf } from '../../core/loaded';
import { Toasts } from '../../core/toast';
import { dateLabel, initials } from '../../me/labels';
import { BadgeCatalogItemDto, BadgeProgressDto, ReviewBadgeProgressRequest } from './leader.models';

/** One application in the queue, as the web's skills-review-page builds it. */
interface ReviewItem {
  key: string;
  memberKey: string;
  memberName: string;
  monogram: string;
  badgeId: string;
  badgeTitle: string;
  badge: BadgeCatalogItemDto | null;
  submittedAtUtc: string | null;
}

/**
 * The kurin's queue of skills waiting for a leader's word. A row opens the application (the skill,
 * what it asks of the seeker, who applied); «Підтвердити» or «Відхилити» asks for an optional note
 * and takes the row off the queue.
 */
@Component({
  selector: 'app-skills-review',
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
    IonList,
    IonListHeader,
    IonItemGroup,
    IonItem,
    IonLabel,
    IonNote,
    IonAvatar,
    IonSkeletonText,
    IonModal,
  ],
  styles: `
    .note {
      margin: 24px 20px;
      color: var(--lk-muted);
      text-align: center;
    }
    ion-avatar {
      width: 40px;
      height: 40px;
      background: var(--lk-primary);
      color: var(--lk-on-primary);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 15px;
      font-weight: 700;
    }
    ion-label h3 {
      font-weight: 600;
      color: var(--lk-ink);
    }
    .lead {
      padding: 4px 20px 0;
    }
    .lead h2 {
      margin: 8px 0 4px;
      font-size: 22px;
      line-height: 28px;
      font-weight: 700;
      color: var(--lk-ink);
    }
    .lead p {
      margin: 0;
      color: var(--lk-muted);
    }
    .requirements {
      white-space: pre-line;
      font-size: 15px;
      line-height: 22px;
      color: var(--lk-ink);
      padding: 12px 0;
    }
    .actions {
      display: grid;
      gap: 8px;
      padding: 8px 16px 32px;
    }
  `,
  template: `
    <ion-header [translucent]="true">
      <ion-toolbar>
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/kurin" text="" /></ion-buttons>
        <ion-title>Вмілості</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content [fullscreen]="true">
      <ion-refresher slot="fixed" [disabled]="!canReview() || !kurinKey" (ionRefresh)="refresh($event)">
        <ion-refresher-content />
      </ion-refresher>
      <ion-header collapse="condense">
        <ion-toolbar>
          <ion-title size="large">Вмілості</ion-title>
        </ion-toolbar>
      </ion-header>

      @if (!kurinKey) {
        <p class="note">Ти ще не в курені.</p>
      } @else if (!canReview()) {
        <p class="note">Немає доступу до модерації вмілостей.</p>
      } @else {
        @switch (queue().state) {
          @case ('loading') {
            <ion-list [inset]="true">
              <ion-item-group>
                @for (row of [1, 2, 3]; track row) {
                  <ion-item>
                    <ion-label>
                      <ion-skeleton-text [animated]="true" style="width: 50%" />
                      <ion-skeleton-text [animated]="true" style="width: 70%" />
                    </ion-label>
                  </ion-item>
                }
              </ion-item-group>
            </ion-list>
          }
          @case ('failed') {
            <p class="note">{{ failedText }}</p>
          }
          @default {
            @if (items().length) {
              <ion-list [inset]="true">
                <ion-list-header><ion-label>{{ countLabel() }}</ion-label></ion-list-header>
                <ion-item-group>
                  @for (item of items(); track item.key) {
                    <ion-item [button]="true" [detail]="true" (click)="selected.set(item)" data-testid="review-item">
                      <ion-avatar slot="start" aria-hidden="true">{{ item.monogram }}</ion-avatar>
                      <ion-label class="ion-text-wrap">
                        <h3>{{ item.badgeTitle }}</h3>
                        <p>{{ item.memberName }}</p>
                      </ion-label>
                      @if (item.submittedAtUtc) { <ion-note slot="end">{{ shortDay(item.submittedAtUtc) }}</ion-note> }
                    </ion-item>
                  }
                </ion-item-group>
              </ion-list>
            } @else {
              <p class="note">Усе розглянуто: заявок на вмілості немає.</p>
            }
          }
        }
      }

      <ion-modal
        [isOpen]="selected() !== null"
        [initialBreakpoint]="1"
        [breakpoints]="[0, 1]"
        (didDismiss)="selected.set(null)"
      >
        <ng-template>
          @if (selected(); as item) {
            <ion-header>
              <ion-toolbar>
                <ion-title>Заявка</ion-title>
                <ion-buttons slot="end">
                  <ion-button (click)="selected.set(null)">Закрити</ion-button>
                </ion-buttons>
              </ion-toolbar>
            </ion-header>
            <ion-content>
              <div class="lead">
                <h2>{{ item.badgeTitle }}</h2>
                @if (item.badge?.specialization) {
                  <p>{{ item.badge?.specialization }}{{ item.badge?.level ? ' · рівень ' + item.badge?.level : '' }}</p>
                }
              </div>
              <ion-list [inset]="true">
                <ion-item-group>
                  <ion-item [button]="true" [detail]="true" (click)="openMember(item)">
                    <ion-avatar slot="start" aria-hidden="true">{{ item.monogram }}</ion-avatar>
                    <ion-label>
                      <h3>{{ item.memberName }}</h3>
                      <p>{{ item.submittedAtUtc ? 'Подано ' + fullDay(item.submittedAtUtc) : 'Очікує підтвердження' }}</p>
                    </ion-label>
                  </ion-item>
                </ion-item-group>
              </ion-list>
              @if (item.badge?.seekerRequirements) {
                <ion-list [inset]="true">
                  <ion-list-header><ion-label>Вимоги</ion-label></ion-list-header>
                  <ion-item-group>
                    <ion-item>
                      <div class="requirements">{{ item.badge?.seekerRequirements }}</div>
                    </ion-item>
                  </ion-item-group>
                </ion-list>
              }
              <div class="actions">
                <ion-button expand="block" [disabled]="busy()" (click)="review(item, true)">Підтвердити</ion-button>
                <ion-button expand="block" fill="clear" class="lk-danger" color="danger" [disabled]="busy()" (click)="review(item, false)">
                  Відхилити
                </ion-button>
              </div>
            </ion-content>
          }
        </ng-template>
      </ion-modal>
    </ion-content>
  `,
})
export class SkillsReviewPage implements OnInit {
  private readonly api = inject(Api);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly alerts = inject(AlertController);
  private readonly toasts = inject(Toasts);

  protected readonly failedText = FAILED_TEXT;
  protected readonly kurinKey = this.auth.user()?.kurinKey ?? null;
  protected readonly queue = signal<Loaded<ReviewItem[]>>({ state: 'loading' });
  protected readonly selected = signal<ReviewItem | null>(null);
  protected readonly busy = signal(false);

  /** The web's gate (PermissionService.canReviewSkills): Виховник and above, or an admin. */
  protected readonly canReview = computed(() => {
    const user = this.auth.user();
    const has = (prefix: string) => (user?.permissions ?? []).some((p) => p.startsWith(prefix));
    return !!user && (user.isAdmin || has('Group:Update') || has('Group:Manage:KurinWide'));
  });

  protected readonly items = computed(() => valueOf(this.queue()) ?? []);
  protected readonly countLabel = computed(() => queueCountLabel(this.items().length));

  ngOnInit(): void {
    if (this.kurinKey && this.canReview()) void this.load();
  }

  protected async refresh(event: Event): Promise<void> {
    await this.load();
    await (event.target as HTMLIonRefresherElement).complete();
  }

  protected shortDay(utc: string): string {
    const date = new Date(utc);
    return `${String(date.getDate()).padStart(2, '0')}.${String(date.getMonth() + 1).padStart(2, '0')}`;
  }

  protected fullDay(utc: string): string {
    return dateLabel(new Date(utc));
  }

  protected async openMember(item: ReviewItem): Promise<void> {
    this.selected.set(null);
    await this.router.navigate(['/tabs/kurin/member', item.memberKey]);
  }

  /** As the web's dialog: a sentence to confirm, an optional note, then the word goes to the API. */
  protected async review(item: ReviewItem, isApproved: boolean): Promise<void> {
    if (this.busy()) return;
    const verb = isApproved ? 'Підтвердити' : 'Відхилити';
    const alert = await this.alerts.create({
      header: isApproved ? 'Підтвердження вмілості' : 'Відхилення вмілості',
      message: `${verb} вмілість «${item.badgeTitle}» для ${item.memberName}?`,
      inputs: [
        {
          name: 'note',
          type: 'textarea',
          placeholder: 'Нотатка (необовʼязково)',
          attributes: { maxlength: 500, 'aria-label': 'Нотатка' },
        },
      ],
      buttons: [
        { text: 'Скасувати', role: 'cancel' },
        { text: verb, role: isApproved ? 'confirm' : 'destructive' },
      ],
    });
    await alert.present();
    const { role, data } = await alert.onDidDismiss<{ values?: { note?: string } }>();
    if (role !== 'confirm' && role !== 'destructive') return;
    const note = data?.values?.note?.trim() || null;
    await this.send(item, { isApproved, note });
  }

  private async send(item: ReviewItem, request: ReviewBadgeProgressRequest): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.post(`member/${item.memberKey}/badges/${encodeURIComponent(item.badgeId)}/review`, request);
      this.remove(item);
      await this.toasts.show(
        request.isApproved ? `Вмілість «${item.badgeTitle}» підтверджено.` : `Вмілість «${item.badgeTitle}» відхилено.`,
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        // Someone else got there first: the queue catches up.
        this.remove(item);
        await this.toasts.show('Заявку вже опрацьовано. Список оновлено.');
      } else {
        await this.toasts.show(
          apiErrorText(error, 'Не вдалося виконати дію. Спробуй ще раз.', { Forbidden: 'Немає доступу до модерації цієї заявки.' }),
          'danger',
        );
      }
    } finally {
      this.busy.set(false);
    }
  }

  private remove(item: ReviewItem): void {
    this.selected.set(null);
    this.queue.set({ state: 'ready', value: this.items().filter((current) => current.key !== item.key) });
  }

  private async load(): Promise<void> {
    const kurinKey = this.kurinKey;
    if (!kurinKey) return;
    // The catalog only names the skills; without it a row shows the skill's id, as on the web.
    const catalog = this.api.get<BadgeCatalogItemDto[]>('catalog/badges', { take: 500 }).catch(() => []);
    const request = Promise.all([this.api.get<BadgeProgressDto[]>(`kurin/${kurinKey}/badges/review`), catalog]).then(
      ([progresses, badges]) => toItems(progresses, badges),
    );
    await settle(request, this.queue);
  }
}

function toItems(progresses: BadgeProgressDto[], badges: BadgeCatalogItemDto[]): ReviewItem[] {
  const byId = new Map(badges.map((badge) => [badge.id, badge]));
  return progresses
    .map((progress) => {
      const first = progress.memberFirstName?.trim() ?? '';
      const last = progress.memberLastName?.trim() ?? '';
      const badge = byId.get(progress.badgeId) ?? null;
      return {
        key: `${progress.memberKey}:${progress.badgeId}`,
        memberKey: progress.memberKey,
        memberName: [first, last].filter(Boolean).join(' ') || 'Учасник',
        monogram: initials(first, last) || '?',
        badgeId: progress.badgeId,
        badgeTitle: badge?.title ?? progress.badgeId,
        badge,
        submittedAtUtc: progress.submittedAtUtc,
      };
    })
    .sort((a, b) => time(b.submittedAtUtc) - time(a.submittedAtUtc));
}

function time(utc: string | null): number {
  return utc ? new Date(utc).getTime() : 0;
}

/** «3 заявки на розгляді», as the web counts them. */
function queueCountLabel(count: number): string {
  const rest = count % 10;
  const teens = count % 100 >= 11 && count % 100 <= 14;
  let noun = 'заявок';
  if (!teens && rest === 1) noun = 'заявка';
  else if (!teens && rest >= 2 && rest <= 4) noun = 'заявки';
  return `${count} ${noun} на розгляді`;
}
