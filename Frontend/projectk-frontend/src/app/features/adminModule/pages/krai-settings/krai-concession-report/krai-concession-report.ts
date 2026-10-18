import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
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

  readonly quarterOptions = quartersUpTo(currentQuarter());
  readonly quarter = signal(quarterKey(currentQuarter()));
  readonly report = signal<KraiConcessionReportDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);

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
      const quarter = this.quarterOptions.find(o => o.value === this.quarter())?.quarter;
      if (code && quarter) {
        this.load(code, quarter);
      }
    });
  }

  kurinLabel(number: number | null): string {
    return number === null ? 'Підготовчий курінь' : `Курінь ${number}`;
  }

  private load(code: string, quarter: QuarterDto): void {
    this.loading.set(true);
    this.failed.set(false);
    this.profiles.concessionReport(code, quarter).subscribe({
      next: report => {
        this.report.set(report);
        this.loading.set(false);
      },
      error: () => {
        this.report.set(null);
        this.loading.set(false);
        this.failed.set(true);
      }
    });
  }
}

/** The quarter running now by the calendar; the край's own clock moves it by a day at most. */
function currentQuarter(): QuarterDto {
  const now = new Date();
  return { year: now.getFullYear(), number: Math.floor(now.getMonth() / 3) + 1 };
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
