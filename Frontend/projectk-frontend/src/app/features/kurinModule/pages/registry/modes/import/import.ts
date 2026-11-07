import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TableModule } from '@openng/optimus-ui/table';
import { ButtonModule } from '@openng/optimus-ui/button';
import { SelectModule } from '@openng/optimus-ui/select';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { TagModule } from '@openng/optimus-ui/tag';
import { MessageModule } from '@openng/optimus-ui/message';

import { KurinService } from '../../../../services/kurin-service/kurin.service';
import { AuthService } from '../../../../../authModule/services/auth-service/auth.service';
import {
  ApplyRosterRequest,
  ColumnMapping,
  FIELD_OPTIONS,
  REQUIRED_FIELDS,
  requiredFieldName,
  RosterColumnPreview,
  RosterField,
  RosterImportReport,
  RowOutcome,
  SheetRow,
  optionByValue,
  optionValueOf
} from './import-model';

type Step = 'upload' | 'map' | 'review' | 'done';

@Component({
  selector: 'app-roster-import',
  imports: [
    TableModule,
    ButtonModule,
    SelectModule,
    ToggleSwitchModule,
    TagModule,
    MessageModule,
    FormsModule
  ],
  templateUrl: './import.html',
  styleUrl: './import.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RosterImportComponent implements OnInit {
  private readonly kurinService = inject(KurinService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly step = signal<Step>('upload');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly columns = signal<RosterColumnPreview[]>([]);
  readonly rows = signal<SheetRow[]>([]);
  readonly chosen = signal<Record<number, string>>({});
  readonly createMissingGroups = signal(true);
  readonly report = signal<RosterImportReport | null>(null);

  readonly fieldOptions = FIELD_OPTIONS;
  readonly fileName = signal<string | null>(null);

  /** Скільки рядків файлу без заголовка — це те, що людина порівнює зі своєю таблицею. */
  readonly rowCount = computed(() => this.rows().length);

  /** Обов'язкові поля, які ще ніде не вибрані. Поки список не порожній, далі не пускаємо. */
  readonly unmappedRequired = computed(() => {
    const used = new Set(Object.values(this.chosen()).map(value => optionByValue(value).field));
    return REQUIRED_FIELDS.filter(field => !used.has(field));
  });

  readonly unmappedRequiredNames = computed(() => this.unmappedRequired().map(requiredFieldName));

  readonly canProceed = computed(() => this.unmappedRequired().length === 0);

  private kurinKey = '';

  ngOnInit(): void {
    this.authService.getAuthState().subscribe(state => {
      if (state?.kurinKey) {
        this.kurinKey = state.kurinKey;
      }
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.kurinKey) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.fileName.set(file.name);

    this.kurinService.previewRoster(this.kurinKey, file).subscribe({
      next: preview => {
        this.columns.set(preview.columns);
        this.rows.set(preview.rows);
        this.chosen.set(Object.fromEntries(
          preview.columns.map(column => [
            column.index,
            optionValueOf(column.suggestedField, column.suggestedLevel)
          ])
        ));
        this.step.set('map');
        this.busy.set(false);
      },
      error: () => {
        this.error.set('Не вдалося прочитати файл. Це має бути .xlsx з одним аркушем.');
        this.busy.set(false);
        input.value = '';
      }
    });
  }

  onMappingChange(index: number, value: string): void {
    this.chosen.update(current => ({ ...current, [index]: value }));
  }

  /**
   * Сухий прогін. Показує, що станеться, тим самим кодом, який потім це й зробить — тож на екрані
   * не прогноз, а результат, який ще не записали.
   */
  review(): void {
    this.run(true, () => this.step.set('review'));
  }

  apply(): void {
    this.run(false, () => this.step.set('done'));
  }

  private run(dryRun: boolean, then: () => void): void {
    if (!this.kurinKey || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    const request: ApplyRosterRequest = {
      rows: this.rows(),
      mapping: this.mapping(),
      createMissingGroups: this.createMissingGroups(),
      dryRun
    };

    this.kurinService.importRoster(this.kurinKey, request).subscribe({
      next: report => {
        this.report.set(report);
        this.busy.set(false);
        then();
      },
      error: () => {
        this.error.set('Імпорт не пройшов. Нічого не записано.');
        this.busy.set(false);
      }
    });
  }

  private mapping(): ColumnMapping[] {
    return Object.entries(this.chosen())
      .map(([index, value]) => {
        const option = optionByValue(value);
        return { index: Number(index), field: option.field, level: option.level };
      })
      .filter(column => column.field !== RosterField.Ignore);
  }

  backToMapping(): void {
    this.step.set('map');
    this.report.set(null);
  }

  startOver(): void {
    this.step.set('upload');
    this.columns.set([]);
    this.rows.set([]);
    this.chosen.set({});
    this.report.set(null);
    this.fileName.set(null);
  }

  openRegistry(): void {
    this.router.navigate(['/kurin/registry']);
  }

  chosenFor(index: number): string {
    return this.chosen()[index] ?? RosterField.Ignore;
  }

  outcomeLabel(outcome: RowOutcome): string {
    switch (outcome) {
      case 'Created': return 'Нова людина';
      case 'Attached': return 'Долучено наявну';
      case 'AlreadyHere': return 'Уже в курені';
      default: return 'Відхилено';
    }
  }

  outcomeSeverity(outcome: RowOutcome): 'success' | 'info' | 'secondary' | 'danger' {
    switch (outcome) {
      case 'Created': return 'success';
      case 'Attached': return 'info';
      case 'AlreadyHere': return 'secondary';
      default: return 'danger';
    }
  }
}
