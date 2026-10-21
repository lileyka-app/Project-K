import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TagModule } from '@openng/optimus-ui/tag';
import { money, quarterLabel } from '../../../duesModule/functions/dues-format.function';
import { MyDuesDto } from '../../models/me.dto';
import { kurinLabel } from '../../../../shared/functions/kurin-label.function';

export interface DuesRow {
  dues: MyDuesDto;
  standing: 'ok' | 'debt' | 'surplus';
  amount: string;
  note: string;
  quarter: string;
}

function standingOf(balance: number): DuesRow['standing'] {
  if (balance === 0) {
    return 'ok';
  }
  return balance < 0 ? 'debt' : 'surplus';
}

const AMOUNT: Record<DuesRow['standing'], (balance: number, currency: string, locale: string) => string> = {
  ok: () => 'Сплачено',
  debt: (balance, currency, locale) => money(balance, currency, locale),
  surplus: (balance, currency, locale) => `+${money(balance, currency, locale)}`
};

const NOTE: Record<DuesRow['standing'], string> = {
  ok: 'боргу немає',
  debt: 'борг — віддай скарбникові гуртка',
  surplus: 'надлишок — піде на наступний квартал'
};

/**
 * What the person owes, or has over, in every kurin that charges them. Debt is said plainly and in
 * red, and the way to settle it is in the line itself: the скарбник of their гурток.
 */
@Component({
  selector: 'app-my-dues-tile',
  imports: [SkeletonModule, TagModule],
  templateUrl: './my-dues-tile.html',
  styleUrl: './my-dues-tile.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MyDuesTileComponent {
  readonly dues = input<MyDuesDto[]>([]);
  readonly loading = input(false);
  readonly failed = input(false);
  readonly namesKurin = input(false);

  readonly rows = computed<DuesRow[]>(() =>
    this.dues().map(dues => {
      const standing = standingOf(dues.balance);
      return {
        dues,
        standing,
        amount: AMOUNT[standing](dues.balance, dues.currency, dues.numberLocale),
        note: NOTE[standing],
        quarter: quarterLabel({ year: dues.quarterYear, number: dues.quarterNumber })
      };
    })
  );

  readonly owing = computed(() => this.rows().filter(r => r.standing === 'debt').length);

  kurinLabel(row: DuesRow): string {
    return kurinLabel(row.dues.kurin.kurinNumber);
  }

  rate(row: DuesRow): string | null {
    return row.dues.quarterRate === null ? null : `${money(row.dues.quarterRate, row.dues.currency, row.dues.numberLocale)} за квартал`;
  }
}
