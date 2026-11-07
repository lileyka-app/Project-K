import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { MultiSelectModule } from '@openng/optimus-ui/multiselect';
import { MessageModule } from '@openng/optimus-ui/message';
import { KurinTagService } from '../../services/kurin-tag-service/kurin-tag.service';
import { KurinTagChipsComponent } from '../kurin-tag-chips/kurin-tag-chips';
import { KURIN_TAG_ERROR_TEXT } from '../../models/kurin-tag.dto';

/**
 * Теги людини на картці: плашки для всіх, хто їх бачить, і «Змінити теги» для того, кому курінь
 * дав право ставити теги. Ліміт на людину перевіряє й форма, і сервер.
 */
@Component({
  selector: 'app-member-tags-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, DialogModule, MultiSelectModule, MessageModule, KurinTagChipsComponent],
  templateUrl: './member-tags-editor.html',
  styleUrl: './member-tags-editor.css'
})
export class MemberTagsEditorComponent implements OnInit {
  private readonly tagService = inject(KurinTagService);

  readonly memberKey = input.required<string>();
  readonly tagKeys = input<readonly string[]>([]);
  /** Нові теги людини після збереження. */
  readonly changed = output<string[]>();

  protected readonly enabled = this.tagService.enabled;
  protected readonly canManage = this.tagService.canManage;
  protected readonly options = computed(() => this.tagService.current().tags);
  protected readonly max = computed(() => this.tagService.current().maxPerMember);

  protected readonly dialogVisible = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected selected: string[] = [];

  ngOnInit(): void {
    this.tagService.ensureCurrent();
  }

  protected open(): void {
    this.selected = [...this.tagKeys()];
    this.error.set(null);
    this.dialogVisible.set(true);
  }

  protected save(): void {
    if (this.selected.length > this.max()) {
      this.error.set(KURIN_TAG_ERROR_TEXT['TooManyTags']);
      return;
    }

    this.saving.set(true);
    this.tagService.setMemberTags(this.memberKey(), this.selected).subscribe({
      next: keys => {
        this.saving.set(false);
        this.dialogVisible.set(false);
        this.changed.emit(keys);
      },
      error: (failure: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(KURIN_TAG_ERROR_TEXT[failure.error?.error] ?? 'Не вдалося зберегти теги.');
      }
    });
  }
}
