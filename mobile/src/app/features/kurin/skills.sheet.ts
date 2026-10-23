import { Component, ElementRef, OnInit, computed, inject, signal } from '@angular/core';
import {
  ActionSheetController,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemGroup,
  IonLabel,
  IonList,
  IonListHeader,
  IonSearchbar,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { ribbon } from 'ionicons/icons';
import { apiErrorText } from '../../core/api';
import { Toasts } from '../../core/toast';
import { canSubmitSkill, existingSkillLabel, progressStatus, shortDate, skillsSummary, SkillView, utcDate } from './kurin.labels';
import { BadgeCatalogItemDto, BadgeProgressDto } from './kurin.models';
import { KurinApi, ProtectedImages } from './kurin.service';

const REVIEW_ERRORS = {
  Conflict: 'Стан вмілості вже змінено. Дані оновлено.',
};

/**
 * «Усі вмілості» (web member-card dialog): confirmed and on review. A reviewer confirms one on
 * review or takes a confirmed one off; whoever may change the card adds one from the catalogue.
 * Dismisses with `true` when anything changed, so the card reads them again.
 */
@Component({
  selector: 'app-skills-sheet',
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonList,
    IonListHeader,
    IonItem,
    IonItemGroup,
    IonLabel,
    IonIcon,
    IonSpinner,
  ],
  styles: `
    .thumb {
      width: 36px;
      height: 36px;
      object-fit: contain;
      margin-inline-end: 12px;
    }
    ion-icon.thumb {
      border-radius: 50%;
      box-sizing: border-box;
      padding: 8px;
      background: var(--lk-primary-50);
      color: var(--lk-primary);
    }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button (click)="close()">Готово</ion-button>
        </ion-buttons>
        <ion-title>Усі вмілості</ion-title>
        @if (canEdit) {
          <ion-buttons slot="end">
            <ion-button (click)="add()" data-testid="add-skill">Додати</ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true" data-testid="confirmed-skills">
        <ion-list-header><ion-label>Підтверджені ({{ summary().confirmed.length }})</ion-label></ion-list-header>
        <ion-item-group>
          @for (skill of summary().confirmed; track skill.badgeId) {
            <ion-item>
              @if (image(skill); as src) { <img class="thumb" slot="start" [src]="src" alt="" /> }
              @else { <ion-icon class="thumb" slot="start" name="ribbon" aria-hidden="true" /> }
              <ion-label class="ion-text-wrap">
                <h3>{{ skill.title }}</h3>
                <p>Підтверджено: {{ when(skill.reviewedAtUtc) }}</p>
              </ion-label>
              @if (canReview) {
                <ion-button slot="end" fill="clear" color="danger" size="default" [disabled]="busy() !== null" (click)="remove(skill)">
                  @if (busy() === skill.badgeId) { <ion-spinner name="crescent" /> } @else { Зняти }
                </ion-button>
              }
            </ion-item>
          } @empty {
            <ion-item><ion-label>Підтверджених вмілостей поки немає.</ion-label></ion-item>
          }
        </ion-item-group>
      </ion-list>

      <ion-list [inset]="true" data-testid="pending-skills">
        <ion-list-header><ion-label>Очікують підтвердження ({{ summary().pending.length }})</ion-label></ion-list-header>
        <ion-item-group>
          @for (skill of summary().pending; track skill.badgeId) {
            <ion-item>
              @if (image(skill); as src) { <img class="thumb" slot="start" [src]="src" alt="" /> }
              @else { <ion-icon class="thumb" slot="start" name="ribbon" aria-hidden="true" /> }
              <ion-label class="ion-text-wrap">
                <h3>{{ skill.title }}</h3>
                <p>Подано: {{ when(skill.submittedAtUtc) }}</p>
              </ion-label>
              @if (canReview) {
                <ion-button slot="end" fill="clear" size="default" [disabled]="busy() !== null" (click)="approve(skill)">
                  @if (busy() === skill.badgeId) { <ion-spinner name="crescent" /> } @else { Підтвердити }
                </ion-button>
              }
            </ion-item>
          } @empty {
            <ion-item><ion-label>Немає вмілостей, що очікують підтвердження.</ion-label></ion-item>
          }
        </ion-item-group>
      </ion-list>
    </ion-content>
  `,
})
export class SkillsSheet implements OnInit {
  private readonly data = inject(KurinApi);
  private readonly images = inject(ProtectedImages);
  private readonly modals = inject(ModalController);
  private readonly sheets = inject(ActionSheetController);
  private readonly toasts = inject(Toasts);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  // Set by ModalController's componentProps (assigned, not bound: these are plain fields).
  memberKey = '';
  canEdit = false;
  canReview = false;
  catalog: BadgeCatalogItemDto[] = [];
  /** The вмілості the kurin's край offers; «Додати» picks from these. */
  offered: BadgeCatalogItemDto[] = [];
  progress: BadgeProgressDto[] = [];

  private readonly list = signal<BadgeProgressDto[]>([]);
  private changed = false;
  protected readonly busy = signal<string | null>(null);
  protected readonly summary = computed(() => skillsSummary(this.list(), this.catalog));

  constructor() {
    addIcons({ ribbon });
  }

  ngOnInit(): void {
    this.list.set(this.progress);
  }

  protected image(skill: SkillView): string | null {
    return this.images.badge(skill.imagePath);
  }

  protected when(value: string | null): string {
    return value ? shortDate(utcDate(value)) : 'дата відсутня';
  }

  /**
   * Its own modal, not the top one: right after «Додати» the catalogue may still be leaving, and a
   * dismiss of the top overlay would land on it and leave this sheet open over the card.
   */
  protected close(): void {
    const own = this.host.closest('ion-modal');
    if (own) void own.dismiss(this.changed);
    else void this.modals.dismiss(this.changed);
  }

  protected approve(skill: SkillView): Promise<void> {
    return this.review(skill, true, `Вмілість «${skill.title}» підтверджено.`);
  }

  protected async remove(skill: SkillView): Promise<void> {
    const sheet = await this.sheets.create({
      header: `Видалити підтверджену вмілість «${skill.title}»?`,
      buttons: [
        { text: 'Видалити', role: 'destructive', data: 'remove' },
        { text: 'Скасувати', role: 'cancel' },
      ],
    });
    await sheet.present();
    const { data } = await sheet.onWillDismiss();
    if (data === 'remove') await this.review(skill, false, `Вмілість «${skill.title}» знято з підтверджених.`);
  }

  protected async add(): Promise<void> {
    const modal = await this.modals.create({
      component: AddSkillSheet,
      componentProps: { memberKey: this.memberKey, catalog: this.offered, progress: this.list() },
      presentingElement: this.host.closest('ion-modal') ?? undefined,
    });
    await modal.present();
    const { data } = await modal.onWillDismiss<boolean>();
    if (data) {
      this.changed = true;
      await this.reload();
    }
  }

  private async review(skill: SkillView, isApproved: boolean, done: string): Promise<void> {
    if (this.busy()) return;
    this.busy.set(skill.badgeId);
    try {
      await this.data.reviewBadge(this.memberKey, skill.badgeId, isApproved);
      this.changed = true;
      await this.toasts.show(done);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося виконати дію. Спробуй ще раз.', REVIEW_ERRORS), 'danger');
    } finally {
      this.busy.set(null);
    }
    await this.reload();
  }

  private async reload(): Promise<void> {
    try {
      this.list.set(await this.data.badgeProgress(this.memberKey));
    } catch {
      // The list keeps what it showed; the card reads again on close.
    }
  }
}

/**
 * «Додати вмілість»: the catalogue with a search over title, specialization and country, a page of
 * 12 at a time; a skill already on review or confirmed cannot be added again. Dismisses with `true`
 * once one was submitted.
 */
@Component({
  selector: 'app-add-skill-sheet',
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonSearchbar,
    IonList,
    IonItem,
    IonItemGroup,
    IonLabel,
    IonIcon,
    IonSpinner,
  ],
  styles: `
    .thumb {
      width: 40px;
      height: 40px;
      object-fit: contain;
      margin-inline-end: 12px;
    }
    ion-icon.thumb {
      border-radius: 50%;
      box-sizing: border-box;
      padding: 9px;
      background: var(--lk-primary-50);
      color: var(--lk-primary);
    }
    .more {
      padding: 0 16px 24px;
    }
    ion-searchbar {
      padding-inline: 12px;
    }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button (click)="close(false)">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>Додати вмілість</ion-title>
      </ion-toolbar>
      <ion-toolbar>
        <ion-searchbar
          placeholder="Назва, спеціалізація або країна"
          [debounce]="150"
          (ionInput)="search($any($event).detail.value ?? '')"
          data-testid="skill-search"
        />
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list [inset]="true">
        <ion-item-group>
          @for (badge of visible(); track badge.id) {
            <ion-item data-testid="catalog-skill">
              @if (image(badge); as src) { <img class="thumb" slot="start" [src]="src" alt="" /> }
              @else { <ion-icon class="thumb" slot="start" name="ribbon" aria-hidden="true" /> }
              <ion-label class="ion-text-wrap">
                <h3>{{ badge.title }}</h3>
                <p>{{ badge.specialization }} · {{ badge.country }}</p>
                @if (existing(badge.id); as status) { <p>{{ existingLabel(status) }}</p> }
              </ion-label>
              <ion-button
                slot="end"
                fill="clear"
                size="default"
                [disabled]="!canSubmit(badge.id) || submitting() !== null"
                (click)="submit(badge)"
              >
                @if (submitting() === badge.id) { <ion-spinner name="crescent" /> }
                @else { {{ existing(badge.id) === 'Rejected' ? 'Подати знову' : 'Додати' }} }
              </ion-button>
            </ion-item>
          } @empty {
            <ion-item><ion-label>Нічого не знайдено за цим запитом.</ion-label></ion-item>
          }
        </ion-item-group>
      </ion-list>
      @if (filtered().length > shown()) {
        <div class="more">
          <ion-button expand="block" fill="clear" (click)="shown.set(shown() + pageSize)">Показати ще</ion-button>
        </div>
      }
    </ion-content>
  `,
})
export class AddSkillSheet {
  private readonly data = inject(KurinApi);
  private readonly images = inject(ProtectedImages);
  private readonly modals = inject(ModalController);
  private readonly toasts = inject(Toasts);

  // Set by ModalController's componentProps.
  memberKey = '';
  catalog: BadgeCatalogItemDto[] = [];
  progress: BadgeProgressDto[] = [];

  protected readonly pageSize = 12;
  protected readonly existingLabel = existingSkillLabel;
  private readonly query = signal('');
  protected readonly shown = signal(this.pageSize);
  protected readonly submitting = signal<string | null>(null);

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const all = [...this.catalog].sort((a, b) => a.title.localeCompare(b.title, 'uk'));
    if (!q) return all;
    return all.filter(
      (b) => b.title.toLowerCase().includes(q) || b.specialization.toLowerCase().includes(q) || b.country.toLowerCase().includes(q),
    );
  });
  protected readonly visible = computed(() => this.filtered().slice(0, this.shown()));
  private readonly statuses = computed(() => new Map(this.progress.map((p) => [p.badgeId, progressStatus(p.status)])));

  constructor() {
    addIcons({ ribbon });
  }

  protected search(value: string): void {
    this.query.set(value);
    this.shown.set(this.pageSize);
  }

  protected image(badge: BadgeCatalogItemDto): string | null {
    return this.images.badge(badge.imagePath);
  }

  protected existing(badgeId: string) {
    return this.statuses().get(badgeId) ?? null;
  }

  protected canSubmit(badgeId: string): boolean {
    return canSubmitSkill(this.existing(badgeId));
  }

  protected close(submitted: boolean): void {
    void this.modals.dismiss(submitted);
  }

  protected async submit(badge: BadgeCatalogItemDto): Promise<void> {
    if (this.submitting() || !this.canSubmit(badge.id)) return;
    this.submitting.set(badge.id);
    try {
      await this.data.submitBadge(this.memberKey, badge.id);
      await this.toasts.show('Вмілість подано на підтвердження.');
      this.close(true);
    } catch (error) {
      await this.toasts.show(apiErrorText(error, 'Не вдалося подати вмілість. Спробуй ще раз.'), 'danger');
    } finally {
      this.submitting.set(null);
    }
  }
}
