import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MultiSelectModule } from '@openng/optimus-ui/multiselect';
import { MessageModule } from '@openng/optimus-ui/message';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { KurinTagService } from '../../services/kurin-tag-service/kurin-tag.service';
import { KURIN_TAG_ERROR_TEXT, KurinTagDto, TAG_COLOR_NAMES } from '../../models/kurin-tag.dto';

/** Людина, якій можна поставити тег: хто зараз у курені. */
export interface TagCandidate {
  memberKey: string;
  name: string;
  tagKeys: readonly string[];
}

/**
 * Теги куреня для того, кому курінь дав право ними керувати: завести, перейменувати, перефарбувати,
 * поставити кільком людям одразу й видалити. Колір — лише з палітри.
 */
@Component({
  selector: 'app-kurin-tag-manager',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, DialogModule, InputTextModule, MultiSelectModule, MessageModule],
  templateUrl: './kurin-tag-manager.html',
  styleUrl: './kurin-tag-manager.css'
})
export class KurinTagManagerComponent {
  private readonly tagService = inject(KurinTagService);
  private readonly confirmation = inject(ConfirmationService);

  readonly kurinKey = input.required<string>();
  readonly candidates = input<readonly TagCandidate[]>([]);

  protected readonly tags = computed(() => this.tagService.current().tags);
  protected readonly palette = computed(() => this.tagService.current().palette);
  protected readonly max = computed(() => this.tagService.current().maxPerMember);
  protected readonly colorNames = TAG_COLOR_NAMES;

  protected readonly editVisible = signal(false);
  protected readonly peopleVisible = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected editing: KurinTagDto | null = null;
  protected name = '';
  protected colorHex = '';
  protected peopleTag: KurinTagDto | null = null;
  protected people: string[] = [];

  /** Хто вже має найбільше тегів і без цього тегу не може отримати ще один. */
  protected readonly full = computed(() => new Set(
    this.candidates().filter(candidate => candidate.tagKeys.length >= this.max()).map(candidate => candidate.memberKey)));

  protected openCreate(): void {
    this.editing = null;
    this.name = '';
    this.colorHex = this.palette()[0] ?? '';
    this.error.set(null);
    this.editVisible.set(true);
  }

  protected openEdit(tag: KurinTagDto): void {
    this.editing = tag;
    this.name = tag.name;
    this.colorHex = tag.colorHex;
    this.error.set(null);
    this.editVisible.set(true);
  }

  protected saveTag(): void {
    const name = this.name.trim();
    if (!name) {
      this.error.set('Впиши назву тегу.');
      return;
    }

    const request$: Observable<KurinTagDto> = this.editing
      ? this.tagService.updateTag(this.kurinKey(), this.editing.kurinTagKey, name, this.colorHex)
      : this.tagService.createTag(this.kurinKey(), name, this.colorHex);
    this.run(request$, () => this.editVisible.set(false));
  }

  protected openPeople(tag: KurinTagDto): void {
    this.peopleTag = tag;
    this.people = this.candidates().filter(candidate => candidate.tagKeys.includes(tag.kurinTagKey)).map(candidate => candidate.memberKey);
    this.error.set(null);
    this.peopleVisible.set(true);
  }

  /** У списку людей недоступні ті, хто вже має ліміт тегів і ще не має цього. */
  protected peopleOptions(): { memberKey: string; name: string; disabled: boolean }[] {
    const tagKey = this.peopleTag?.kurinTagKey;
    return this.candidates().map(candidate => ({
      memberKey: candidate.memberKey,
      name: candidate.name,
      disabled: this.full().has(candidate.memberKey) && !(tagKey && candidate.tagKeys.includes(tagKey))
    }));
  }

  protected savePeople(): void {
    if (!this.peopleTag) {
      return;
    }
    this.run(
      this.tagService.setTagMembers(this.kurinKey(), this.peopleTag.kurinTagKey, this.people),
      () => this.peopleVisible.set(false));
  }

  protected remove(tag: KurinTagDto): void {
    const carriers = tag.memberCount ?? 0;
    this.confirmation.confirm({
      header: 'Видалити тег?',
      message: carriers
        ? `Тег «${tag.name}» зникне в ${carriers} ${this.peopleWord(carriers)}. Повернути його не вийде.`
        : `Тег «${tag.name}» зникне. Повернути його не вийде.`,
      acceptLabel: 'Видалити',
      rejectLabel: 'Скасувати',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', variant: 'text' },
      accept: () => this.tagService.deleteTag(this.kurinKey(), tag.kurinTagKey).subscribe({ error: () => undefined })
    });
  }

  private run<T>(request$: Observable<T>, done: () => void): void {
    this.saving.set(true);
    this.error.set(null);
    request$.subscribe({
      next: () => {
        this.saving.set(false);
        done();
      },
      error: (failure: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(KURIN_TAG_ERROR_TEXT[failure.error?.error] ?? 'Не вдалося зберегти.');
      }
    });
  }

  // «у 1 людини», «у 3 людей», «у 5 людей».
  private peopleWord(count: number): string {
    return count % 10 === 1 && count % 100 !== 11 ? 'людини' : 'людей';
  }
}
