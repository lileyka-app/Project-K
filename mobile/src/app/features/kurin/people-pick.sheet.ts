import { Component, ElementRef, OnInit, computed, inject, signal } from '@angular/core';
import {
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
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { checkmark, checkmarkCircle, ellipseOutline } from 'ionicons/icons';
import { filterMembers, fullName } from './kurin.labels';
import { MemberLookupDto } from './kurin.models';
import { MemberAvatar } from './member-avatar';

/** One person to pick: `key` is what comes back (a member key, or a user key for виховники). */
export interface PickPerson {
  key: string;
  member: MemberLookupDto;
  /** A second line: where they are now, or why they are listed. */
  note?: string | null;
}

/** Below this many people a search field is more clutter than help (as on the гурток page). */
const SEARCH_FROM = 9;

/**
 * Picks people from a list, as the web's multiselect and select do: one (a провід office, with
 * «Не обсаджено») or several (виховники, people to add to a гурток). Dismisses with the picked
 * keys, or null when cancelled.
 */
@Component({
  selector: 'app-people-pick-sheet',
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonSearchbar,
    IonList,
    IonListHeader,
    IonItem,
    IonItemGroup,
    IonLabel,
    IonIcon,
    MemberAvatar,
  ],
  styles: `
    app-member-avatar {
      margin-inline-end: 12px;
    }
    .mark {
      font-size: 24px;
      color: var(--lk-faint);
    }
    .mark.on {
      color: var(--lk-primary);
    }
    ion-searchbar {
      padding-inline: 12px;
    }
    .hint {
      margin: 0 20px;
      font-size: 13px;
      color: var(--lk-muted);
    }
  `,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button (click)="close(null)">Скасувати</ion-button>
        </ion-buttons>
        <ion-title>{{ title }}</ion-title>
        @if (multiple) {
          <ion-buttons slot="end">
            <ion-button [strong]="true" [disabled]="!allowEmpty && !picked().length" (click)="close(picked())" data-testid="pick-done">
              {{ doneLabel }}{{ multiple && picked().length ? ' (' + picked().length + ')' : '' }}
            </ion-button>
          </ion-buttons>
        }
      </ion-toolbar>
    </ion-header>
    <ion-content>
      @if (people.length >= searchFrom) {
        <ion-searchbar
          placeholder="Пошук"
          [value]="query()"
          [debounce]="150"
          (ionInput)="query.set($any($event).detail.value ?? '')"
          data-testid="pick-search"
        />
      }
      <ion-list [inset]="true" data-testid="pick-list">
        @if (header) { <ion-list-header><ion-label>{{ header }}</ion-label></ion-list-header> }
        <ion-item-group>
          @if (noneLabel && !query()) {
            <ion-item [button]="true" [detail]="false" (click)="pick(null)" data-testid="pick-none">
              <ion-label color="medium">{{ noneLabel }}</ion-label>
              @if (!picked().length) { <ion-icon slot="end" name="checkmark" color="primary" aria-label="Обрано" /> }
            </ion-item>
          }
          @for (person of shown(); track person.key) {
            <ion-item
              [button]="true"
              [detail]="false"
              (click)="pick(person.key)"
              [attr.aria-pressed]="multiple ? isPicked(person.key) : null"
              [class.picked]="isPicked(person.key)"
              data-testid="pick-person"
            >
              <app-member-avatar slot="start" [photo]="person.member.profilePhotoUrl" [firstName]="person.member.firstName" [lastName]="person.member.lastName" />
              <ion-label class="ion-text-wrap">
                <h3>{{ name(person.member) }}</h3>
                @if (person.note) { <p>{{ person.note }}</p> }
              </ion-label>
              @if (multiple) {
                <ion-icon slot="end" class="mark" [class.on]="isPicked(person.key)" [name]="isPicked(person.key) ? 'checkmark-circle' : 'ellipse-outline'" aria-hidden="true" />
              } @else if (isPicked(person.key)) {
                <ion-icon slot="end" name="checkmark" color="primary" aria-label="Обрано" />
              }
            </ion-item>
          } @empty {
            <ion-item><ion-label class="ion-text-wrap">{{ query() ? 'Нічого не знайшлось.' : emptyText }}</ion-label></ion-item>
          }
        </ion-item-group>
      </ion-list>
      @if (hint) { <p class="hint">{{ hint }}</p> }
    </ion-content>
  `,
})
export class PeoplePickSheet implements OnInit {
  private readonly modals = inject(ModalController);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  // Set by ModalController's componentProps.
  title = '';
  header: string | null = null;
  people: PickPerson[] = [];
  selected: string[] = [];
  multiple = false;
  /** A single pick may also be nobody: this row says so (e.g. «Не обсаджено»). */
  noneLabel: string | null = null;
  doneLabel = 'Готово';
  /** A multiple pick may come back empty (виховники: take everyone off). */
  allowEmpty = true;
  emptyText = 'Нікого немає.';
  hint: string | null = null;

  protected readonly searchFrom = SEARCH_FROM;
  protected readonly query = signal('');
  protected readonly picked = signal<string[]>([]);
  protected readonly shown = computed(() => {
    const members = filterMembers(this.people.map((p) => p.member), this.query());
    const keep = new Set(members.map((m) => m.memberKey));
    return this.people.filter((p) => keep.has(p.member.memberKey));
  });

  constructor() {
    addIcons({ checkmark, checkmarkCircle, ellipseOutline });
  }

  ngOnInit(): void {
    this.picked.set([...this.selected]);
  }

  protected name(member: MemberLookupDto): string {
    return fullName(member);
  }

  protected isPicked(key: string): boolean {
    return this.picked().includes(key);
  }

  protected pick(key: string | null): void {
    if (!this.multiple) {
      this.close(key ? [key] : []);
      return;
    }
    if (!key) return;
    this.picked.update((keys) => (keys.includes(key) ? keys.filter((k) => k !== key) : [...keys, key]));
  }

  /** Its own modal, not the top one: this sheet can sit over another (the провід form). */
  protected close(keys: string[] | null): void {
    const own = this.host.closest('ion-modal');
    if (own) void own.dismiss(keys);
    else void this.modals.dismiss(keys);
  }
}
