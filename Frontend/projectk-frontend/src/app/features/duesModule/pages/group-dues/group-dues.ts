import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { SelectModule } from '@openng/optimus-ui/select';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE, currencySymbol, duesAmountLabel, isIncomingDues, money, quarterKey, quarterLabel, quarterShort, signedMoney } from '../../functions/dues-format.function';
import { fromQuarterOptions } from '../../functions/from-quarter-options.function';
import { KurinRateDialogComponent } from '../../components/kurin-rate-dialog/kurin-rate-dialog';
import { KraiConcessionGrantsComponent } from '../../components/krai-concession-grants/krai-concession-grants';
import { DuesRegisterComponent } from '../../components/dues-register/dues-register';
import {
  DUES_ENTRY_KIND_LABELS,
  DUES_PAYMENT_METHOD_LABELS,
  DuesEntryKind,
  DuesUpstream,
  GROUP_DUES_KINDS,
  paysStanytsia
} from '../../models/dues.enums';
import {
  DuesAccountDto,
  DuesEntryDto,
  GroupDuesDto,
  QuarterDto,
  SetKurinDuesRateRequest,
  UpsertDuesEntryRequest
} from '../../models/group-dues.dto';
import { DuesService } from '../../services/dues-service/dues.service';
import { DuesEntryDialogComponent } from './components/dues-entry-dialog/dues-entry-dialog';
import { PageTab, PageTabsComponent } from '../../../../shared/page-tabs/page-tabs';
import { PageGroups } from '../../../kurinModule/services/page-groups/page-groups.service';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';

/**
 * The гурток's box: who paid what by quarter, every operation with the впорядник's mark, and what
 * the box holds. The server sends it all in one read, and every write is followed by that read
 * again — balances move together, so nothing here is patched by hand.
 */
@Component({
  selector: 'app-group-dues',
  imports: [
    PageTabsComponent,
    DatePipe, FormsModule, ButtonModule, TableModule, TagModule, SelectModule, CheckboxModule, DialogModule,
    InputTextModule, ToggleSwitchModule, TooltipModule, SkeletonModule, ConfirmDialogModule,
    EmptyStateComponent, DuesEntryDialogComponent, KurinRateDialogComponent, KraiConcessionGrantsComponent, DuesRegisterComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './group-dues.html',
  styleUrl: './group-dues.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GroupDuesComponent implements OnInit {
  private readonly pageGroups = inject(PageGroups);
  private readonly authState = inject(AuthService);
  /** Вкладка куреня і вкладки гуртків — одне місце в меню; курінь той, у якому людина діє. */
  readonly tabs = signal<PageTab[]>([]);

  private readonly route = inject(ActivatedRoute);
  private readonly dues = inject(DuesService);
  private readonly confirmation = inject(ConfirmationService);

  readonly quarterLabel = quarterLabel;
  readonly quarterShort = quarterShort;
  readonly methodLabels = DUES_PAYMENT_METHOD_LABELS;

  /** What this kurin counts its вкладка in, from its край's profile. */
  readonly currency = computed(() => this.data()?.currency ?? DEFAULT_CURRENCY);
  readonly numberLocale = computed(() => this.data()?.numberLocale ?? DEFAULT_NUMBER_LOCALE);
  readonly upstream = computed(() => this.data()?.upstream ?? DuesUpstream.Stanytsia);
  /** A край paid straight has no станиця part, unless an old rate still sets one. */
  readonly showStanytsia = (stanytsiaFull: number): boolean => stanytsiaFull > 0 || paysStanytsia(this.upstream());
  readonly currencySymbol = computed(() => currencySymbol(this.currency()));
  readonly money = (amount: number): string => money(amount, this.currency(), this.numberLocale());
  readonly signedMoney = (amount: number): string => signedMoney(amount, this.currency(), this.numberLocale());

  groupKey = '';

  readonly data = signal<GroupDuesDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);

  readonly selectedYear = signal<number | null>(null);
  readonly personFilter = signal<string | null>(null);
  readonly kindFilter = signal<DuesEntryKind | 'all'>('all');
  readonly quarterFilter = signal<string>('all');

  readonly year = computed(() => {
    const data = this.data();
    if (!data) {
      return null;
    }
    return data.years.find(y => y.startYear === this.selectedYear()) ?? data.years[0] ?? null;
  });

  readonly yearOptions = computed(() => (this.data()?.years ?? []).map(y => ({ label: y.label, value: y.startYear })));

  readonly kindFilterOptions = [
    { label: 'Усі види', value: 'all' as const },
    ...GROUP_DUES_KINDS.map(kind => ({ label: DUES_ENTRY_KIND_LABELS[kind], value: kind }))
  ];

  readonly quarterFilterOptions = computed(() => [
    { label: 'Усі квартали', value: 'all' },
    ...(this.year()?.quarters ?? []).map(q => ({ label: quarterLabel(q), value: quarterKey(q) }))
  ]);

  readonly fromQuarterOptions = computed(() => {
    const data = this.data();
    return data ? fromQuarterOptions(data.years, data.currentQuarter) : [];
  });

  readonly currentRows = computed(() => (this.data()?.accounts ?? []).filter(a => a.standing === 'Current'));
  readonly debtors = computed(() => (this.data()?.accounts ?? []).filter(a => a.balance < 0).length);

  readonly filteredEntries = computed(() => {
    const data = this.data();
    if (!data) {
      return [];
    }
    const person = this.personFilter();
    const kind = this.kindFilter();
    const quarter = this.quarterFilter();
    return data.entries.filter(entry =>
      (!person || entry.membershipKey === person)
      && (kind === 'all' || entry.kind === kind)
      && (quarter === 'all' || quarterKey(this.quarterOfDate(entry.occurredOn)) === quarter));
  });

  readonly personFilterName = computed(() => {
    const key = this.personFilter();
    return key ? this.data()?.accounts.find(a => a.membershipKey === key)?.fullName ?? null : null;
  });

  readonly groupRateNow = computed(() => this.effectiveRate(this.data()?.groupRates ?? [], r => r.fromQuarter)?.groupShare ?? null);
  readonly kurinRateNow = computed(() => this.effectiveRate(this.data()?.kurinRates ?? [], r => r.fromQuarter));
  readonly quarterTotal = computed(() => {
    const kurin = this.kurinRateNow();
    const group = this.groupRateNow() ?? 0;
    return (kurin?.stanytsiaFull ?? 0) + (kurin?.kraiFull ?? 0) + (kurin?.kurinShare ?? 0) + group;
  });

  readonly entryDialogVisible = signal(false);
  readonly entryToEdit = signal<DuesEntryDto | null>(null);
  readonly savingEntry = signal(false);
  readonly entryError = signal<string | null>(null);
  readonly busyEntryKey = signal<string | null>(null);

  readonly rateDialogVisible = signal(false);
  readonly rateFromQuarter = signal<string>('');
  readonly rateShare = signal<number | null>(null);
  readonly savingRate = signal(false);

  readonly kurinRateDialogVisible = signal(false);
  readonly savingKurinRate = signal(false);

  readonly concessionDialogVisible = signal(false);
  readonly concessionTarget = signal<DuesAccountDto | null>(null);
  readonly concessionFromQuarter = signal<string>('');
  readonly concessionOn = signal(true);
  readonly savingConcession = signal(false);
  /** The person in the пільга dialog as the last read has them, so a saved grant shows at once. */
  readonly concessionAccount = computed(() => {
    const target = this.concessionTarget();
    return this.data()?.accounts.find(a => a.membershipKey === target?.membershipKey) ?? target;
  });

  ngOnInit(): void {
    const kurinKey = this.authState.getAuthStateValue()?.kurinKey;
    if (kurinKey) {
      this.pageGroups.dues(kurinKey).subscribe(tabs => this.tabs.set(tabs));
    }
    this.route.paramMap.subscribe(params => {
      this.groupKey = params.get('groupKey') ?? '';
      this.personFilter.set(null);
      this.load();
    });
  }

  load(): void {
    if (!this.groupKey) {
      return;
    }
    this.loading.set(true);
    this.loadFailed.set(false);
    this.dues.getGroupDues(this.groupKey).subscribe({
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

  togglePerson(account: DuesAccountDto): void {
    this.personFilter.set(this.personFilter() === account.membershipKey ? null : account.membershipKey);
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

  methodLabel(entry: DuesEntryDto): string {
    const from = DUES_PAYMENT_METHOD_LABELS[entry.method];
    return entry.counterMethod ? `${from} → ${DUES_PAYMENT_METHOD_LABELS[entry.counterMethod]}` : from;
  }

  canEdit(entry: DuesEntryDto): boolean {
    // A transfer the kurin received is the kurin's money now: changing it here would change its box.
    return !!this.data()?.viewer.canKeep && !entry.isVerified && !entry.receivedAtUtc;
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
      ? this.dues.updateEntry(this.groupKey, editing.duesEntryKey, request)
      : this.dues.createEntry(this.groupKey, request);
    call.subscribe({
      next: () => {
        this.savingEntry.set(false);
        this.entryDialogVisible.set(false);
        this.load();
      },
      error: (error: unknown) => {
        this.savingEntry.set(false);
        this.entryError.set(lockedReason(error) ?? failureDetail(error, 'Не вдалося записати. Спробуй ще раз.'));
      }
    });
  }

  confirmDelete(entry: DuesEntryDto): void {
    this.confirmation.confirm({
      header: 'Видалити операцію?',
      message: `${DUES_ENTRY_KIND_LABELS[entry.kind]} на ${this.money(entry.amount)} зникне з каси. Запис про неї лишиться в історії змін.`,
      icon: 'pi pi-trash',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => {
        this.busyEntryKey.set(entry.duesEntryKey);
        this.dues.deleteEntry(this.groupKey, entry.duesEntryKey).subscribe({
          next: () => { this.busyEntryKey.set(null); this.load(); },
          error: () => this.busyEntryKey.set(null)
        });
      }
    });
  }

  setVerified(entry: DuesEntryDto, isVerified: boolean): void {
    this.busyEntryKey.set(entry.duesEntryKey);
    this.dues.setEntryVerified(this.groupKey, entry.duesEntryKey, isVerified).subscribe({
      next: () => { this.busyEntryKey.set(null); this.load(); },
      error: () => this.busyEntryKey.set(null)
    });
  }

  openRateDialog(): void {
    const data = this.data();
    this.rateFromQuarter.set(data ? quarterKey(data.currentQuarter) : '');
    this.rateShare.set(this.groupRateNow());
    this.rateDialogVisible.set(true);
  }

  saveRate(): void {
    const from = this.fromQuarterOptions().find(o => o.value === this.rateFromQuarter())?.quarter;
    const share = Number(this.rateShare());
    if (!from || !Number.isFinite(share) || share < 0) {
      return;
    }
    this.savingRate.set(true);
    this.dues.setGroupRate(this.groupKey, { fromQuarter: from, groupShare: share }).subscribe({
      next: () => { this.savingRate.set(false); this.rateDialogVisible.set(false); this.load(); },
      error: () => this.savingRate.set(false)
    });
  }

  saveKurinRate(request: SetKurinDuesRateRequest): void {
    const data = this.data();
    if (!data) {
      return;
    }
    this.savingKurinRate.set(true);
    this.dues.setKurinRate(data.kurinKey, request).subscribe({
      next: () => { this.savingKurinRate.set(false); this.kurinRateDialogVisible.set(false); this.load(); },
      error: () => this.savingKurinRate.set(false)
    });
  }

  openConcessionDialog(account: DuesAccountDto): void {
    const data = this.data();
    this.concessionTarget.set(account);
    this.concessionFromQuarter.set(data ? quarterKey(data.currentQuarter) : '');
    this.concessionOn.set(!account.isConcessionNow);
    this.concessionDialogVisible.set(true);
  }

  saveConcession(): void {
    const target = this.concessionTarget();
    const from = this.fromQuarterOptions().find(o => o.value === this.concessionFromQuarter())?.quarter;
    if (!target || !from) {
      return;
    }
    this.savingConcession.set(true);
    this.dues.setConcession({ groupKey: this.groupKey }, target.membershipKey, { fromQuarter: from, isConcession: this.concessionOn() }).subscribe({
      next: () => { this.savingConcession.set(false); this.load(); },
      error: () => this.savingConcession.set(false)
    });
  }

  private quarterOfDate(date: string): QuarterDto {
    const [year, month] = date.split('-').map(Number);
    return { year, number: Math.floor((month - 1) / 3) + 1 };
  }

  /** The rate in force now: the latest one that started at or before the current quarter. */
  private effectiveRate<T>(rates: T[], from: (rate: T) => QuarterDto): T | null {
    const current = this.data()?.currentQuarter;
    if (!current) {
      return null;
    }
    const index = (q: QuarterDto) => q.year * 4 + q.number;
    return rates
      .filter(r => index(from(r)) <= index(current))
      .sort((a, b) => index(from(b)) - index(from(a)))[0] ?? null;
  }
}

/** Why the server kept an operation as it was, when someone locked it while the dialog was open. */
function lockedReason(error: unknown): string | null {
  const code = error instanceof HttpErrorResponse && error.status === 409 ? (error.error as { error?: unknown } | null)?.error : null;
  if (code === 'TransferReceived') {
    return 'Курінний уже прийняв цю передачу. Змінити її можна, лише коли він зніме підтвердження.';
  }
  return code === 'EntryVerified' ? 'Операцію вже перевірено, її не змінити.' : null;
}
