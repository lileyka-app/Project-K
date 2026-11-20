import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MultiSelectModule } from '@openng/optimus-ui/multiselect';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE, money, quarterLabel, quarterShort, sameQuarter } from '../../functions/dues-format.function';
import { DUES_STANDING_LABELS } from '../../models/dues.enums';
import { DuesAccountDto, DuesAccountQuarterDto, DuesTagDto, PlastYearDto, QuarterDto } from '../../models/group-dues.dto';
import { RegisterRow, registerCsv, registerRow } from './dues-register.function';

type RegisterView = 'summary' | 'quarters';

/** «З боргом» sits among the tags in the filter, keyed apart from any tag. */
const DEBT_FILTER = '__debt__';

/**
 * The treasurer's register, one row per person: what they paid this year, what they owe for quarters
 * already behind them, the running quarter, and how far ahead they are paid. The same table serves a
 * гурток's box and a senior kurin's own, filters by the kurin's tags and goes out as a file.
 */
@Component({
  selector: 'app-dues-register',
  imports: [NgTemplateOutlet, FormsModule, ButtonModule, MultiSelectModule, SelectButtonModule, TableModule, TagModule, TooltipModule, EmptyStateComponent],
  templateUrl: './dues-register.html',
  styleUrl: './dues-register.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DuesRegisterComponent {
  readonly accounts = input.required<DuesAccountDto[]>();
  readonly year = input<PlastYearDto | null>(null);
  readonly currentQuarter = input.required<QuarterDto>();
  readonly tags = input<DuesTagDto[]>([]);
  readonly currency = input(DEFAULT_CURRENCY);
  readonly numberLocale = input(DEFAULT_NUMBER_LOCALE);
  /** Offer the пільга button on each person standing here. */
  readonly canKeep = input(false);
  /** The person the history is narrowed to, marked in the table. */
  readonly selected = input<string | null>(null);
  readonly nameHeader = input('Юнак');
  /** The file's name, without the extension. */
  readonly fileName = input('vkladka');

  readonly rowClick = output<DuesAccountDto>();
  readonly concession = output<DuesAccountDto>();

  readonly quarterShort = quarterShort;
  readonly money = (amount: number): string => money(amount, this.currency(), this.numberLocale());

  readonly view = signal<RegisterView>('summary');
  readonly filter = signal<string[]>([]);

  readonly viewOptions = [
    { label: 'Підсумок', value: 'summary' as const },
    { label: 'По кварталах', value: 'quarters' as const }
  ];

  readonly filterOptions = computed(() => [
    ...this.tags().map(tag => ({ label: tag.name, value: tag.tagKey, colorHex: tag.colorHex })),
    { label: 'З боргом', value: DEBT_FILTER, colorHex: null }
  ]);

  readonly currentLabel = computed(() => quarterLabel(this.currentQuarter()));

  readonly rows = computed<RegisterRow[]>(() => {
    const year = this.year();
    const current = this.currentQuarter();
    return this.accounts().map(account => registerRow(account, year, current));
  });

  readonly filtered = computed(() => {
    const chosen = this.filter();
    const tags = chosen.filter(key => key !== DEBT_FILTER);
    const debtOnly = chosen.includes(DEBT_FILTER);
    return this.rows().filter(row =>
      (!debtOnly || row.account.balance < 0)
      && (tags.length === 0 || tags.some(tag => row.account.tagKeys?.includes(tag))));
  });

  readonly currentRows = computed(() => this.filtered().filter(r => r.account.standing === 'Current'));
  readonly formerRows = computed(() => this.filtered().filter(r => r.account.standing !== 'Current'));

  readonly columnCount = computed(() => this.view() === 'summary' ? 8 : (this.year()?.quarters.length ?? 4) + 3);

  tagsOf(account: DuesAccountDto): DuesTagDto[] {
    const keys = account.tagKeys ?? [];
    return this.tags().filter(tag => keys.includes(tag.tagKey));
  }

  activeGrantNames(account: DuesAccountDto): string[] {
    return (account.grants ?? []).filter(g => g.isActiveNow).map(g => g.name);
  }

  standingLabel(account: DuesAccountDto): string | null {
    return DUES_STANDING_LABELS[account.standing];
  }

  cellFor(row: RegisterRow, quarter: QuarterDto): DuesAccountQuarterDto | null {
    return row.account.quarters.find(q => sameQuarter(q.quarter, quarter)) ?? null;
  }

  paidUntilLabel(row: RegisterRow): string | null {
    return row.paidUntil ? quarterLabel(row.paidUntil) : null;
  }

  download(): void {
    const csv = registerCsv(this.filtered(), this.tags(), this.nameHeader(), this.currentQuarter());
    // The byte-order mark makes Excel read the file as UTF-8.
    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.fileName()}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
}
