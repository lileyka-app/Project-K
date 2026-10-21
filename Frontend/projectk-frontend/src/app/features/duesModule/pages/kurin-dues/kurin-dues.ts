import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { SelectModule } from '@openng/optimus-ui/select';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { KurinRateDialogComponent } from '../../components/kurin-rate-dialog/kurin-rate-dialog';
import { DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE, currencySymbol, duesAmountLabel, isIncomingDues, money, quarterKey, quarterLabel, quarterShort, signedMoney } from '../../functions/dues-format.function';
import {
  DUES_ENTRY_KIND_LABELS,
  DUES_PAYMENT_METHOD_LABELS,
  DuesEntryKind,
  DuesUpstream,
  KURIN_DUES_KINDS,
  kurinDuesKinds,
  paysKrai,
  paysStanytsia
} from '../../models/dues.enums';
import {
  DuesEntryDto,
  DuesTransferDto,
  KurinDuesDto,
  KurinDuesQuarterDto,
  KurinGroupHandoverDto,
  QuarterDto,
  SetKurinDuesRateRequest,
  UpsertDuesEntryRequest
} from '../../models/group-dues.dto';
import { DuesService } from '../../services/dues-service/dues.service';
import { DuesEntryDialogComponent } from '../group-dues/components/dues-entry-dialog/dues-entry-dialog';

/**
 * The kurin's box: what each гурток has collected for the станиця and the kurin and handed over,
 * the transfers waiting for the скарбник's word that they arrived, and the kurin's own operations
 * with the Звʼязковий's mark. One read, reloaded after every write.
 */
@Component({
  selector: 'app-kurin-dues',
  imports: [
    DatePipe, FormsModule, RouterLink, ButtonModule, TableModule, TagModule, SelectModule, CheckboxModule,
    ToggleSwitchModule, TooltipModule, SkeletonModule, ConfirmDialogModule,
    EmptyStateComponent, DuesEntryDialogComponent, KurinRateDialogComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './kurin-dues.html',
  styleUrl: './kurin-dues.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KurinDuesComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly dues = inject(DuesService);
  private readonly confirmation = inject(ConfirmationService);

  readonly quarterLabel = quarterLabel;
  readonly quarterShort = quarterShort;
  readonly methodLabels = DUES_PAYMENT_METHOD_LABELS;
  readonly upstream = computed(() => this.data()?.upstream ?? DuesUpstream.Stanytsia);
  readonly kurinKinds = computed(() => kurinDuesKinds(this.upstream()));

  kurinKey = '';

  readonly data = signal<KurinDuesDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);

  /** What this kurin counts its вкладка in, from its край's profile. */
  readonly currency = computed(() => this.data()?.currency ?? DEFAULT_CURRENCY);
  readonly numberLocale = computed(() => this.data()?.numberLocale ?? DEFAULT_NUMBER_LOCALE);
  readonly currencySymbol = computed(() => currencySymbol(this.currency()));
  readonly money = (amount: number): string => money(amount, this.currency(), this.numberLocale());
  readonly signedMoney = (amount: number): string => signedMoney(amount, this.currency(), this.numberLocale());

  readonly selectedYear = signal<number | null>(null);
  readonly year = computed(() => {
    const data = this.data();
    if (!data) {
      return null;
    }
    return data.years.find(y => y.startYear === this.selectedYear()) ?? data.years[0] ?? null;
  });
  readonly yearOptions = computed(() => (this.data()?.years ?? []).map(y => ({ label: y.label, value: y.startYear })));

  /** The chosen year's quarters that have charges, newest first — a quarter nobody was charged in has no row. */
  readonly yearQuarters = computed(() => {
    const year = this.year();
    const quarters = this.data()?.quarters ?? [];
    if (!year) {
      return [];
    }
    return year.quarters
      .map(q => quarters.find(x => x.quarter.year === q.year && x.quarter.number === q.number) ?? null)
      .filter((q): q is KurinDuesQuarterDto => q !== null)
      .reverse();
  });

  readonly groupFilter = signal<string | null>(null);
  readonly pendingOnly = signal(false);
  readonly kindFilter = signal<DuesEntryKind | 'all'>('all');
  readonly quarterFilter = signal<string>('all');

  readonly kindFilterOptions = [
    { label: 'Усі види', value: 'all' as const },
    ...KURIN_DUES_KINDS.map(kind => ({ label: DUES_ENTRY_KIND_LABELS[kind], value: kind }))
  ];

  readonly quarterFilterOptions = computed(() => [
    { label: 'Усі квартали', value: 'all' },
    ...(this.data()?.years ?? []).flatMap(y => y.quarters).map(q => ({ label: quarterLabel(q), value: quarterKey(q) }))
  ]);

  readonly rateNow = computed(() => {
    const data = this.data();
    if (!data) {
      return null;
    }
    const index = (q: QuarterDto) => q.year * 4 + q.number;
    return data.rates
      .filter(r => index(r.fromQuarter) <= index(data.currentQuarter))
      .sort((a, b) => index(b.fromQuarter) - index(a.fromQuarter))[0] ?? null;
  });

  readonly pendingTransfers = computed(() => (this.data()?.transfers ?? []).filter(t => !t.isReceived));

  readonly filteredTransfers = computed(() => {
    const group = this.groupFilter();
    return (this.data()?.transfers ?? []).filter(t =>
      (!group || t.groupKey === group) && (!this.pendingOnly() || !t.isReceived));
  });

  readonly groupFilterName = computed(() => {
    const key = this.groupFilter();
    return key ? this.data()?.groups.find(g => g.groupKey === key)?.groupName ?? null : null;
  });

  readonly filteredEntries = computed(() => {
    const kind = this.kindFilter();
    const quarter = this.quarterFilter();
    return (this.data()?.entries ?? []).filter(entry =>
      (kind === 'all' || entry.kind === kind)
      && (quarter === 'all' || quarterKey(this.quarterOfDate(entry.occurredOn)) === quarter));
  });

  /** The край takes money straight, or the kurin still has some of it or sent it before: the край's money shows apart. */
  readonly hasKrai = computed(() => {
    const d = this.data();
    return !!d && (paysKrai(this.upstream()) || d.toKrai !== 0 || d.sentToKrai !== 0 || d.rates.some(r => r.kraiFull > 0));
  });
  /** The same for the станиця: a край paid straight has none, unless old money is still on the way. */
  readonly hasStanytsia = computed(() => {
    const d = this.data();
    return !!d && (paysStanytsia(this.upstream()) || d.toStanytsia !== 0 || d.sentToStanytsia !== 0 || d.rates.some(r => r.stanytsiaFull > 0));
  });
  readonly upTo = computed(() => [this.hasStanytsia() ? 'станицю' : '', this.hasKrai() ? 'край' : ''].filter(Boolean).join(' і '));
  readonly outstandingTotal = computed(() => (this.data()?.groups ?? []).reduce((sum, g) => sum + g.outstanding, 0));

  readonly entryDialogVisible = signal(false);
  readonly entryToEdit = signal<DuesEntryDto | null>(null);
  readonly savingEntry = signal(false);
  readonly entryError = signal<string | null>(null);
  readonly busyKey = signal<string | null>(null);

  readonly rateDialogVisible = signal(false);
  readonly savingRate = signal(false);

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.kurinKey = params.get('kurinKey') ?? '';
      this.groupFilter.set(null);
      this.load();
    });
  }

  load(): void {
    if (!this.kurinKey) {
      return;
    }
    this.loading.set(true);
    this.loadFailed.set(false);
    this.dues.getKurinDues(this.kurinKey).subscribe({
      next: data => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
      }
    });
  }

  toggleGroup(group: KurinGroupHandoverDto): void {
    this.groupFilter.set(this.groupFilter() === group.groupKey ? null : group.groupKey);
  }

  setReceived(transfer: DuesTransferDto, isReceived: boolean): void {
    this.busyKey.set(transfer.duesEntryKey);
    this.dues.setTransferReceived(this.kurinKey, transfer.duesEntryKey, isReceived).subscribe({
      next: () => { this.busyKey.set(null); this.load(); },
      error: () => this.busyKey.set(null)
    });
  }

  kindLabel(entry: DuesEntryDto): string {
    return DUES_ENTRY_KIND_LABELS[entry.kind];
  }

  amountLabel(entry: DuesEntryDto): string {
    return duesAmountLabel(entry, this.currency(), this.numberLocale());
  }

  isIncoming(entry: DuesEntryDto): boolean | null {
    return isIncomingDues(entry);
  }

  methodLabel(entry: { method: DuesEntryDto['method']; counterMethod?: DuesEntryDto['counterMethod'] }): string {
    const from = DUES_PAYMENT_METHOD_LABELS[entry.method];
    return entry.counterMethod ? `${from} → ${DUES_PAYMENT_METHOD_LABELS[entry.counterMethod]}` : from;
  }

  canEdit(entry: DuesEntryDto): boolean {
    return !!this.data()?.viewer.canKeep && !entry.isVerified;
  }

  openEntryDialog(entry: DuesEntryDto | null = null): void {
    this.entryToEdit.set(entry);
    this.entryError.set(null);
    this.entryDialogVisible.set(true);
  }

  saveEntry(request: UpsertDuesEntryRequest): void {
    const editing = this.entryToEdit();
    this.savingEntry.set(true);
    this.entryError.set(null);
    const call = editing
      ? this.dues.updateKurinEntry(this.kurinKey, editing.duesEntryKey, request)
      : this.dues.createKurinEntry(this.kurinKey, request);
    call.subscribe({
      next: () => {
        this.savingEntry.set(false);
        this.entryDialogVisible.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.savingEntry.set(false);
        this.entryError.set(failureDetail(error, 'Не вдалося записати. Спробуй ще раз.'));
      }
    });
  }

  confirmDelete(entry: DuesEntryDto): void {
    this.confirmation.confirm({
      header: 'Видалити операцію',
      message: `${DUES_ENTRY_KIND_LABELS[entry.kind]} на ${this.money(entry.amount)} зникне з каси. Запис про неї лишиться в історії змін.`,
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.busyKey.set(entry.duesEntryKey);
        this.dues.deleteKurinEntry(this.kurinKey, entry.duesEntryKey).subscribe({
          next: () => { this.busyKey.set(null); this.load(); },
          error: () => this.busyKey.set(null)
        });
      }
    });
  }

  setVerified(entry: DuesEntryDto, isVerified: boolean): void {
    this.busyKey.set(entry.duesEntryKey);
    this.dues.setKurinEntryVerified(this.kurinKey, entry.duesEntryKey, isVerified).subscribe({
      next: () => { this.busyKey.set(null); this.load(); },
      error: () => this.busyKey.set(null)
    });
  }

  saveRate(request: SetKurinDuesRateRequest): void {
    this.savingRate.set(true);
    this.dues.setKurinRate(this.kurinKey, request).subscribe({
      next: () => { this.savingRate.set(false); this.rateDialogVisible.set(false); this.load(); },
      error: () => this.savingRate.set(false)
    });
  }

  private quarterOfDate(date: string): QuarterDto {
    const [year, month] = date.split('-').map(Number);
    return { year, number: Math.floor((month - 1) / 3) + 1 };
  }
}
