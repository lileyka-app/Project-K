import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';

import { KraiOption, OnboardingService, WaitlistEntry } from '../../../authModule/services/onboarding-service/onboarding.service';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TagModule } from '@openng/optimus-ui/tag';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { MessageService, ConfirmationService } from '@openng/optimus-ui/api';
import { ProgressBarModule } from '@openng/optimus-ui/progressbar';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { SelectButtonModule } from '@openng/optimus-ui/selectbutton';
import { SelectModule } from '@openng/optimus-ui/select';
import { WaitlistAttentionService } from '../../services/waitlist-attention/waitlist-attention.service';
import {
  getWaitlistStatusLabel,
  getWaitlistStatusSeverity,
  isWaitlistApproved,
  isWaitlistAwaitingDecision,
  isWaitlistInitial,
  WaitlistStatusSeverity
} from '../../functions/waitlist-status.function';
import { LocalUtcDatePipe } from '../../../../shared/pipes/local-utc-date.pipe';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { KurinBranchRequestService } from '../../../kurinModule/services/kurin-branch-request-service/kurin-branch-request.service';
import { KurinBranchRequestDto } from '../../../kurinModule/models/kurin-branch-request.dto';
import { KurinBranch, KurinNumbering } from '../../../kurinModule/models/enums/kurin-branch.enum';
import { kurinBranchesName, kurinGroupTitle } from '../../../../shared/functions/kurin-label.function';

/** Коди помилок схвалення заявки → що сказати людині. */
const APPROVAL_ERROR_TEXT: Record<string, string> = {
  KraiNotFound: 'Такого краю немає в списку.'
};

/** What an inbox row stands for: a person's application, a person a провід added, a kurin's change of улади й числа. */
export type InboxKind = 'join' | 'direct' | 'branch';
export type InboxState = 'waiting' | 'settled' | 'all';

/** One row of the inbox, whichever kind it is, so every kind reads and sorts the same way. */
export interface InboxItem {
  key: string;
  kind: InboxKind;
  title: string;
  /** What the row asks for: the kurin a person claims, or a kurin's улади й числа now → wanted. */
  claim: string | null;
  claimSeverity: 'info' | 'warn' | 'secondary';
  from: string | null;
  details: string[];
  note: string | null;
  submittedAtUtc: string;
  statusLabel: string;
  statusSeverity: WaitlistStatusSeverity;
  waiting: boolean;
  entry?: WaitlistEntry;
  request?: KurinBranchRequestDto;
}

const KIND_LABEL: Record<InboxKind, string> = {
  join: 'Приєднання',
  direct: 'Додано напряму',
  branch: 'Улади й числа'
};

const BRANCH_STATUS: Record<KurinBranchRequestDto['status'], { label: string; severity: WaitlistStatusSeverity }> = {
  Pending: { label: 'Подано', severity: 'info' },
  Approved: { label: 'Схвалено', severity: 'success' },
  Rejected: { label: 'Відхилено', severity: 'danger' }
};

@Component({
  selector: 'app-waitlist-management',
  imports: [
    ButtonModule,
    TagModule,
    TooltipModule,
    ProgressBarModule,
    ConfirmDialogModule,
    DialogModule,
    TextareaModule,
    FormsModule,
    SelectButtonModule,
    SelectModule,
    LocalUtcDatePipe,
    EmptyStateComponent
],
  providers: [ConfirmationService],
  template: `
    <p-confirmDialog />

    <p-dialog [(visible)]="rejectionDialogVisible" header="Відхилити заявку" [modal]="true" [style]="{ width: 'min(92vw, 450px)' }">
        <div class="flex flex-col gap-4">
            <p>Відхилити заявку <strong>{{ selectedEntry?.firstName }} {{ selectedEntry?.lastName }}</strong>?</p>
            <div class="flex flex-col gap-2">
                <label for="note">Причина відмови (не обовʼязково)</label>
                <textarea id="note" pTextarea [(ngModel)]="rejectionNote" rows="3" class="w-full"></textarea>
            </div>
        </div>
        <ng-template pTemplate="footer">
            <p-button label="Скасувати" icon="pi pi-times" text (onClick)="rejectionDialogVisible = false" />
            <p-button label="Відхилити" icon="pi pi-check" severity="danger" (onClick)="confirmReject()" />
        </ng-template>
    </p-dialog>

    <p-dialog [(visible)]="approvalDialogVisible" header="Схвалити заявку" [modal]="true" [style]="{ width: 'min(92vw, 450px)' }">
        <div class="flex flex-col gap-4">
            <p class="m-0">
                Схвалити заявку <strong>{{ approvalEntry?.firstName }} {{ approvalEntry?.lastName }}</strong>?
                На {{ approvalEntry?.email }} піде лист із запрошенням.
            </p>
            @if (approvalEntry?.isKurinLeaderCandidate && krais.length) {
                <div class="flex flex-col gap-2">
                    <label for="approval-krai">Край куреня</label>
                    <p-select
                        inputId="approval-krai"
                        [options]="krais"
                        optionLabel="name"
                        optionValue="code"
                        [(ngModel)]="approvalKrai"
                        [filter]="true"
                        filterBy="name"
                        appendTo="body"
                        styleClass="w-full" />
                    <small class="text-muted-color">
                        Курінь відкриється в цьому краї, і його число має бути вільним саме тут.
                        @if (!approvalEntry.kraiCode && approvalEntry.regionOrCountry) {
                            Заявник написав: «{{ approvalEntry.regionOrCountry }}».
                        }
                    </small>
                </div>
            }
        </div>
        <ng-template pTemplate="footer">
            <p-button label="Скасувати" icon="pi pi-times" text (onClick)="approvalDialogVisible = false" />
            <p-button label="Схвалити" icon="pi pi-check" [loading]="approving" (onClick)="confirmApproval()" />
        </ng-template>
    </p-dialog>

    <section class="lil-page">
    <header class="lil-page-head">
      <div>
        <p class="lil-page-head__eyebrow">Адміністрація</p>
        <h1>Заявки</h1>
      </div>
    </header>

    <section class="kurin-tile" aria-labelledby="inbox-title">
      <div class="lil-tile-head inbox__head">
        <h2 id="inbox-title" class="sr-only">Усі заявки й запити</h2>
        <p-selectButton
          [options]="stateOptions"
          [ngModel]="stateFilter"
          (ngModelChange)="setState($event)"
          optionLabel="label"
          optionValue="value"
          [allowEmpty]="false"
          ariaLabel="Які заявки показати" />
        <p-select
          [options]="kindOptions"
          [ngModel]="kindFilter"
          (ngModelChange)="setKind($event)"
          optionLabel="label"
          optionValue="value"
          ariaLabel="Вид заявки"
          styleClass="inbox__kind" />
      </div>

      @if (loading) {
        <p-progressBar mode="indeterminate" [style]="{ height: '4px' }" />
      } @else if (visibleItems.length) {
        <ul class="inbox">
          @for (item of visibleItems; track item.kind + item.key) {
            <li class="inbox__item">
              <div class="inbox__body">
                <div class="inbox__top">
                  <span class="inbox__kind-label">{{ kindLabel(item.kind) }}</span>
                  <p-tag [severity]="item.statusSeverity" [value]="item.statusLabel" />
                </div>
                <strong class="inbox__title">{{ item.title }}</strong>
                @if (item.claim) {
                  <span class="inbox__claim">
                    @if (item.from) {
                      <span class="text-muted-color">{{ item.from }}</span>
                      <i class="pi pi-arrow-right" aria-hidden="true"></i>
                    }
                    <p-tag [severity]="item.claimSeverity" [value]="item.claim" />
                  </span>
                }
                @if (item.details.length) {
                  <small class="text-muted-color inbox__details">{{ item.details.join(' · ') }}</small>
                }
                @if (item.note) {
                  <p class="m-0 text-sm">«{{ item.note }}»</p>
                }
                <small class="text-muted-color">Подано {{ item.submittedAtUtc | localUtcDate:'short' }}</small>
              </div>
              <div class="inbox__actions">
                @if (item.request; as request) {
                  @if (item.waiting) {
                    <p-button icon="pi pi-check" severity="success" rounded text [loading]="decidingKey === request.kurinBranchRequestKey"
                              (onClick)="decideBranchRequest(request, true)" pTooltip="Схвалити: курінь отримає ці улади й числа" ariaLabel="Схвалити запит" />
                    <p-button icon="pi pi-times" severity="danger" rounded text [disabled]="decidingKey !== null"
                              (onClick)="decideBranchRequest(request, false)" pTooltip="Відхилити" ariaLabel="Відхилити запит" />
                  }
                } @else if (item.entry; as entry) {
                  @if (isInitial(entry.verificationStatus)) {
                    <p-button icon="pi pi-check" severity="success" rounded text
                              (onClick)="approve(entry)" pTooltip="Схвалити й надіслати запрошення" ariaLabel="Схвалити й надіслати запрошення" />
                    <p-button icon="pi pi-times" severity="danger" rounded text
                              (onClick)="reject(entry)" pTooltip="Відхилити" ariaLabel="Відхилити" />
                  }
                  @if (isApproved(entry.verificationStatus)) {
                    <p-button icon="pi pi-refresh" severity="secondary" rounded text
                              (onClick)="resend(entry)" pTooltip="Надіслати запрошення ще раз" ariaLabel="Надіслати запрошення ще раз" />
                  }
                }
              </div>
            </li>
          }
        </ul>
      } @else {
        <app-empty-state
          art="list"
          [title]="stateFilter === 'waiting' ? 'Нічого не чекає' : 'Заявок немає'"
          [body]="stateFilter === 'waiting' ? 'Нові заявки й запити куренів зʼявляться тут.' : 'Тут будуть заявки на приєднання й запити куренів.'" />
      }
    </section>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .inbox__head {
      flex-wrap: wrap;
      gap: 0.75rem;
    }
    :host ::ng-deep .inbox__kind {
      min-width: 12rem;
    }
    .inbox {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .inbox__item {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      padding: 0.75rem 1rem;
      border: 1px solid var(--p-content-border-color);
      border-radius: 12px;
    }
    .inbox__body {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      min-width: 0;
    }
    .inbox__top {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .inbox__kind-label {
      color: var(--p-text-muted-color);
      font-size: var(--lil-text-caption);
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    .inbox__title {
      overflow-wrap: anywhere;
    }
    .inbox__claim {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .inbox__details {
      overflow-wrap: anywhere;
    }
    .inbox__actions {
      display: flex;
      gap: 0.5rem;
      flex-shrink: 0;
    }
  `]
})
export class WaitlistManagementComponent implements OnInit {
  entries: WaitlistEntry[] = [];
  branchRequests: KurinBranchRequestDto[] = [];
  /** Every application and every kurin's request, newest first. */
  items: InboxItem[] = [];
  loading = true;

  /** What waits for a decision by default: that is what the page is opened for. */
  stateFilter: InboxState = 'waiting';
  kindFilter: InboxKind | 'all' = 'all';
  stateOptions: { label: string; value: InboxState }[] = [];
  readonly kindOptions: { label: string; value: InboxKind | 'all' }[] = [
    { label: 'Усі види', value: 'all' },
    { label: KIND_LABEL.join, value: 'join' },
    { label: KIND_LABEL.branch, value: 'branch' },
    { label: KIND_LABEL.direct, value: 'direct' }
  ];
  visibleItems: InboxItem[] = [];

  rejectionDialogVisible = false;
  rejectionNote = '';
  selectedEntry: WaitlistEntry | null = null;

  private readonly onboardingService = inject(OnboardingService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly waitlistAttention = inject(WaitlistAttentionService);
  private readonly branchRequestService = inject(KurinBranchRequestService);

  decidingKey: string | null = null;

  krais: KraiOption[] = [];
  approvalDialogVisible = false;
  approvalEntry: WaitlistEntry | null = null;
  approvalKrai: string | null = null;
  approving = false;

  private entriesLoaded = false;
  private requestsLoaded = false;

  ngOnInit() {
    this.rebuild();
    this.loadEntries();
    this.loadBranchRequests();
    this.onboardingService.getKrais().subscribe({
      next: krais => {
        this.krais = krais;
        this.rebuild();
      },
      // Without the list the rows show what the applicant wrote, and approval keeps their край.
      error: () => undefined
    });
  }

  setState(state: InboxState): void {
    this.stateFilter = state;
    this.applyFilters();
  }

  setKind(kind: InboxKind | 'all'): void {
    this.kindFilter = kind ?? 'all';
    this.applyFilters();
  }

  kindLabel(kind: InboxKind): string {
    return KIND_LABEL[kind];
  }

  kraiName(entry: WaitlistEntry): string {
    return this.krais.find(krai => krai.code === entry.kraiCode)?.name
      ?? entry.kraiCode
      ?? entry.regionOrCountry
      ?? '-';
  }

  loadEntries() {
    this.onboardingService.getWaitlistEntries().subscribe({
      next: (data) => {
        this.entries = data;
        this.entriesLoaded = true;
        this.waitlistAttention.countFrom(data);
        this.rebuild();
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Помилка', detail: 'Не вдалося завантажити заявки' });
        this.entriesLoaded = true;
        this.rebuild();
      }
    });
  }

  loadBranchRequests(): void {
    this.branchRequestService.getAll().subscribe({
      next: requests => {
        this.branchRequests = requests;
        this.requestsLoaded = true;
        this.waitlistAttention.countRequests(requests);
        this.rebuild();
      },
      error: () => {
        this.requestsLoaded = true;
        this.rebuild();
      }
    });
  }

  /** «Курінь ч. 5 УПЮ», from what the applicant claimed: the number alone would read the same in every улад. */
  youthClaim(entry: WaitlistEntry): string {
    const claimed = (entry.claimedKurinNameOrNumber ?? '').trim();
    return /^\d+$/.test(claimed) ? kurinGroupTitle({ number: Number(claimed) }) : `Курінь ${claimed} УПЮ`;
  }

  /** «5-й курінь УСП · 20-й курінь УПС», from what the applicant claimed. */
  seniorClaim(entry: WaitlistEntry): string {
    const branches: KurinNumbering[] = [];
    if (entry.claimedUspNumber != null) branches.push({ branch: KurinBranch.USP, number: entry.claimedUspNumber });
    if (entry.claimedUpsNumber != null) branches.push({ branch: KurinBranch.UPS, number: entry.claimedUpsNumber });
    return kurinBranchesName(branches);
  }

  decideBranchRequest(request: KurinBranchRequestDto, approve: boolean): void {
    this.decidingKey = request.kurinBranchRequestKey;
    const decision = approve
      ? this.branchRequestService.approve(request.kurinBranchRequestKey)
      : this.branchRequestService.reject(request.kurinBranchRequestKey);
    decision.subscribe({
      next: () => {
        this.decidingKey = null;
        this.messageService.add(approve
          ? { severity: 'success', summary: 'Схвалено', detail: `Тепер це ${kurinBranchesName(request.wantedBranches)}.` }
          : { severity: 'info', summary: 'Відхилено', detail: 'Курінь отримає сповіщення.' });
        this.loadBranchRequests();
      },
      error: (error: HttpErrorResponse) => {
        this.decidingKey = null;
        this.messageService.add({
          severity: 'error',
          summary: 'Не вдалося',
          detail: error.error?.error === 'KurinNumberTaken'
            ? 'Це число в цьому уладі вже має інший курінь краю. Відхили запит або домовся з куренем про інше.'
            : 'Запит не вирішено. Спробуй ще раз.'
        });
        this.loadBranchRequests();
      }
    });
  }

  /** A leader's approval opens their kurin, so the admin sees its край and may change it first. */
  approve(entry: WaitlistEntry) {
    this.approvalEntry = entry;
    this.approvalKrai = entry.kraiCode ?? this.krais.find(krai => krai.isDefault)?.code ?? null;
    this.approvalDialogVisible = true;
  }

  confirmApproval() {
    const entry = this.approvalEntry;
    if (!entry || this.approving) return;

    const kraiCode = entry.isKurinLeaderCandidate ? this.approvalKrai : null;
    this.approving = true;
    this.onboardingService.approveWaitlistEntry(entry.waitlistEntryKey, kraiCode).subscribe({
        next: (approval) => {
          this.approving = false;
          this.approvalDialogVisible = false;
          this.messageService.add(approval?.invitationSent === false
            ? { severity: 'warn', summary: 'Схвалено', detail: 'Лист не надіслано. Надішли запрошення ще раз.' }
            : { severity: 'success', summary: 'Схвалено', detail: 'Запрошення надіслано' });
          this.loadEntries();
        },
        error: (err) => {
          this.approving = false;
          this.messageService.add({
            severity: 'error',
            summary: 'Помилка',
            detail: APPROVAL_ERROR_TEXT[err.error?.error] ?? (err.error?.message || 'Не вдалося схвалити')
          });
          this.loadEntries();
        }
      });
  }

  reject(entry: WaitlistEntry) {
    this.selectedEntry = entry;
    this.rejectionNote = '';
    this.rejectionDialogVisible = true;
  }

  confirmReject() {
    if (!this.selectedEntry) return;

    this.onboardingService.rejectWaitlistEntry(this.selectedEntry.waitlistEntryKey, this.rejectionNote).subscribe({
      next: () => {
        this.messageService.add({ severity: 'info', summary: 'Відхилено', detail: 'Заявку відхилено' });
        this.rejectionDialogVisible = false;
        this.loadEntries();
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Помилка', detail: 'Не вдалося відхилити' });
      }
    });
  }

  resend(entry: WaitlistEntry) {
    this.confirmationService.confirm({
        message: `Запрошення піде на ${entry.email} ще раз.`,
        header: 'Надіслати ще раз?',
        icon: 'pi pi-envelope',
        acceptButtonProps: { label: 'Надіслати' },
        rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
        accept: () => {
            this.onboardingService.resendInvitation(entry.waitlistEntryKey).subscribe({
                next: () => {
                  this.messageService.add({ severity: 'success', summary: 'Надіслано', detail: 'Запрошення надіслано ще раз' });
                },
                error: () => {
                  this.messageService.add({ severity: 'error', summary: 'Помилка', detail: 'Не вдалося надіслати' });
                }
              });
        }
    });
  }

  isInitial(status: string | number): boolean {
    return isWaitlistInitial(status);
  }

  isApproved(status: string | number): boolean {
    return isWaitlistApproved(status);
  }

  private rebuild(): void {
    this.loading = !this.entriesLoaded || !this.requestsLoaded;
    this.items = [
      ...this.entries.map(entry => this.fromEntry(entry)),
      ...this.branchRequests.map(request => this.fromRequest(request))
    ].sort((a, b) => Date.parse(b.submittedAtUtc) - Date.parse(a.submittedAtUtc));
    const waiting = this.items.filter(item => item.waiting).length;
    this.stateOptions = [
      { label: waiting ? `Чекають · ${waiting}` : 'Чекають', value: 'waiting' },
      { label: 'Вирішені', value: 'settled' },
      { label: 'Усі', value: 'all' }
    ];
    this.applyFilters();
  }

  private applyFilters(): void {
    this.visibleItems = this.items.filter(item =>
      (this.kindFilter === 'all' || item.kind === this.kindFilter)
      && (this.stateFilter === 'all' || item.waiting === (this.stateFilter === 'waiting')));
  }

  private fromEntry(entry: WaitlistEntry): InboxItem {
    const direct = entry.source === 'DirectAdd';
    const claim = direct ? null
      : entry.isSeniorKurin ? this.seniorClaim(entry)
      : entry.isPreparatoryKurin ? 'Підготовчий курінь'
      : entry.isKurinLeaderCandidate ? this.youthClaim(entry)
      : null;
    return {
      key: entry.waitlistEntryKey,
      kind: direct ? 'direct' : 'join',
      title: `${entry.firstName} ${entry.lastName}`,
      claim,
      claimSeverity: entry.isPreparatoryKurin && !entry.isSeniorKurin ? 'warn' : 'info',
      from: null,
      details: [entry.email, entry.stanytsia, this.kraiName(entry)].filter(part => !!part && part !== '-'),
      note: null,
      submittedAtUtc: entry.requestedAtUtc,
      statusLabel: getWaitlistStatusLabel(entry.verificationStatus),
      statusSeverity: getWaitlistStatusSeverity(entry.verificationStatus),
      waiting: !direct && isWaitlistAwaitingDecision(entry.verificationStatus),
      entry
    };
  }

  private fromRequest(request: KurinBranchRequestDto): InboxItem {
    const pending = request.status === 'Pending';
    const status = BRANCH_STATUS[request.status] ?? BRANCH_STATUS.Pending;
    return {
      key: request.kurinBranchRequestKey,
      kind: 'branch',
      title: request.kurinNamedAfter || kurinBranchesName(request.currentBranches),
      claim: kurinBranchesName(request.wantedBranches),
      claimSeverity: 'info',
      // Once settled the kurin already carries the wanted улади, so «now → wanted» would repeat itself.
      from: pending ? kurinBranchesName(request.currentBranches) : null,
      details: [request.requestedByName || 'Хтось із проводу'],
      note: request.note,
      submittedAtUtc: request.requestedAtUtc,
      statusLabel: status.label,
      statusSeverity: status.severity,
      waiting: pending,
      request
    };
  }
}
