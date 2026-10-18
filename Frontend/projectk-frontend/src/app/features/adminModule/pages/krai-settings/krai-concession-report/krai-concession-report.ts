import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SelectModule } from '@openng/optimus-ui/select';
import { KraiConcessionReportDto } from '../../../../duesModule/models/concession.dto';
import { QuarterDto } from '../../../../duesModule/models/group-dues.dto';
import { DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE, money, quarterKey, quarterLabel } from '../../../../duesModule/functions/dues-format.function';
import { KraiProfileService } from '../../../services/krai-profile-service/krai-profile.service';

/** How many quarters back the report can look: two пластові роки. */
const QUARTERS_BACK = 8;

/**
 * Звіт для краю: скільки людей у його куренях платять менше завдяки кожній пільзі краю і на скільки
 * зменшилася кожна частина вкладки за квартал. Рахується лише пільга, що справді визначила суму.
 */
@Component({
  selector: 'app-krai-concession-report',
  imports: [FormsModule, SelectModule],
  templateUrl: './krai-concession-report.html',
  styleUrl: './krai-concession-report.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KraiConcessionReportComponent {
  private readonly profiles = inject(KraiProfileService);

  readonly kraiCode = input.required<string>();

  /** The quarter running now by the край's clock, as the first report tells it. */
  private readonly current = signal<QuarterDto | null>(null);
  readonly quarterOptions = computed(() => {
    const current = this.current();
    return current ? quartersUpTo(current) : [];
  });
  /** The picked quarter; null until the first report names the running one. */
  readonly quarter = signal<string | null>(null);
  readonly report = signal<KraiConcessionReportDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);

  /** Only the latest request may fill the table: an older answer arriving late is dropped. */
  private request = 0;
  private shownKey: string | null = null;

  readonly money = computed(() => {
    const report = this.report();
    const currency = report?.currency ?? DEFAULT_CURRENCY;
    const locale = report?.numberLocale ?? DEFAULT_NUMBER_LOCALE;
    return (amount: number) => money(amount, currency, locale);
  });

  readonly totalPeople = computed(() => (this.report()?.concessions ?? []).reduce((sum, row) => sum + row.people, 0));
  readonly totalOff = computed(() => (this.report()?.concessions ?? []).reduce((sum, row) => sum + row.reduction.total, 0));

  constructor() {
    effect(() => {
      const code = this.kraiCode();
      const key = this.quarter();
      if (!code || (key !== null && `${code}:${key}` === this.shownKey)) {
        return;
      }
      const quarter = untracked(() => this.quarterOptions()).find(o => o.value === key)?.quarter ?? null;
      this.load(code, quarter);
    });
  }

  kurinLabel(number: number | null): string {
    return number === null ? 'Підготовчий курінь' : `Курінь ${number}`;
  }

  private load(code: string, quarter: QuarterDto | null): void {
    const request = ++this.request;
    this.loading.set(true);
    this.failed.set(false);
    this.profiles.concessionReport(code, quarter).subscribe({
      next: report => {
        if (request !== this.request) {
          return;
        }
        const key = quarterKey(report.quarter);
        this.shownKey = `${code}:${key}`;
        if (!this.current()) {
          this.current.set(report.quarter);
        }
        this.quarter.set(key);
        this.report.set(report);
        this.loading.set(false);
      },
      error: () => {
        if (request !== this.request) {
          return;
        }
        this.report.set(null);
        this.loading.set(false);
        this.failed.set(true);
      }
    });
  }
}

function quartersUpTo(last: QuarterDto): { label: string; value: string; quarter: QuarterDto }[] {
  const options = [];
  let quarter = last;
  for (let i = 0; i < QUARTERS_BACK; i++) {
    options.push({ label: quarterLabel(quarter), value: quarterKey(quarter), quarter });
    quarter = quarter.number === 1 ? { year: quarter.year - 1, number: 4 } : { year: quarter.year, number: quarter.number - 1 };
  }
  return options;
}
