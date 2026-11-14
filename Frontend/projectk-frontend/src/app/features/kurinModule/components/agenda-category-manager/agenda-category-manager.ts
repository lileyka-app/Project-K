import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { SelectModule } from '@openng/optimus-ui/select';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { MessageService } from '@openng/optimus-ui/api';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { AgendaCategoryDto, UpsertAgendaCategoryRequest } from '../../models/agenda';

/** Curated brand-token colours and icons so groups stay inside BRANDBOOK §0 (no ad-hoc colours). */
const BRAND_COLORS = ['#2F855A', '#2B6CB0', '#B7791F', '#9B2C2C', '#6B46C1', '#0E7490', '#4A5568'];
const GROUP_ICONS = [
  { label: 'Табір', value: 'pi pi-sun' },
  { label: 'Захід', value: 'pi pi-flag' },
  { label: 'Сходини', value: 'pi pi-users' },
  { label: 'Вишкіл', value: 'pi pi-compass' },
  { label: 'Свято', value: 'pi pi-star' },
  { label: 'Праця', value: 'pi pi-briefcase' }
];

/**
 * Зв'язковий-only CRUD for event groups (табори/заходи/сходини). Colour and icon come from curated
 * brand-token lists so the calendar stays inside the brandbook. Mutations are gated on the backend too.
 */
@Component({
  selector: 'app-agenda-category-manager',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule, ButtonModule, DialogModule, InputTextModule, InputNumberModule,
    TextareaModule, SelectModule, ToggleSwitchModule
  ],
  templateUrl: './agenda-category-manager.html',
  styleUrl: './agenda-category-manager.css'
})
export class AgendaCategoryManagerComponent implements OnInit {
  private readonly agendaService = inject(AgendaService);
  private readonly messages = inject(MessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly kurinKey = input.required<string>();

  protected readonly categories = signal<AgendaCategoryDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  protected readonly colors = BRAND_COLORS;
  protected readonly icons = GROUP_ICONS;

  protected dialogVisible = false;
  protected editing: AgendaCategoryDto | null = null;
  protected form = this.emptyForm();

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.agendaService.getCategoriesForManagement(this.kurinKey()).subscribe({
      next: categories => {
        this.categories.set(categories);
        this.loading.set(false);
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося завантажити групи подій' });
      }
    });
  }

  openCreate(): void {
    this.editing = null;
    this.form = this.emptyForm();
    this.dialogVisible = true;
  }

  openEdit(category: AgendaCategoryDto): void {
    this.editing = category;
    this.form = { ...category };
    this.dialogVisible = true;
  }

  save(): void {
    if (!this.form.name.trim()) {
      this.messages.add({ severity: 'warn', summary: 'Вкажи назву групи' });
      return;
    }

    this.saving.set(true);
    const request: UpsertAgendaCategoryRequest = {
      ...this.form,
      agendaCategoryKey: this.editing?.agendaCategoryKey ?? null,
      kurinKey: this.kurinKey(),
      name: this.form.name.trim()
    };

    this.agendaService.upsertCategory(request).subscribe({
      next: () => {
        this.saving.set(false);
        this.dialogVisible = false;
        this.messages.add({ severity: 'success', summary: this.editing ? 'Оновлено' : 'Створено' });
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося зберегти групу' });
      }
    });
  }

  remove(category: AgendaCategoryDto): void {
    if (!confirm(`Видалити групу «${category.name}»? Події з неї залишаться, але без групи.`)) {
      return;
    }
    this.agendaService.deleteCategory(this.kurinKey(), category.agendaCategoryKey).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: 'Видалено' });
        this.load();
      },
      error: () => this.messages.add({ severity: 'error', summary: 'Не вдалося видалити' })
    });
  }

  private emptyForm(): UpsertAgendaCategoryRequest {
    return {
      kurinKey: '',
      name: '',
      colorHex: BRAND_COLORS[0],
      icon: GROUP_ICONS[0].value,
      capacity: null,
      waitlistEnabled: false,
      defaultDescription: null,
      rsvpRequired: false,
      defaultDurationMinutes: null,
      reminderLeadMinutes: null,
      isArchived: false,
      isKurinSchedule: false
    };
  }
}
