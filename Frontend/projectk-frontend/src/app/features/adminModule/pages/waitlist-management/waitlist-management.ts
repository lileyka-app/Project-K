import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';

import { KraiOption, OnboardingService, WaitlistEntry, WaitlistEntrySource } from '../../../authModule/services/onboarding-service/onboarding.service';
import { TableModule } from '@openng/optimus-ui/table';
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
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { WaitlistAttentionService } from '../../services/waitlist-attention/waitlist-attention.service';
import {
  getWaitlistStatusLabel,
  getWaitlistStatusSeverity,
  isWaitlistApproved,
  isWaitlistInitial
} from '../../functions/waitlist-status.function';
import { LocalUtcDatePipe } from '../../../../shared/pipes/local-utc-date.pipe';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { KurinBranchRequestService } from '../../../kurinModule/services/kurin-branch-request-service/kurin-branch-request.service';
import { KurinBranchRequestDto } from '../../../kurinModule/models/kurin-branch-request.dto';
import { KurinBranch, KurinNumbering } from '../../../kurinModule/models/enums/kurin-branch.enum';
import { kurinBranchesName, kurinGroupTitle } from '../../../../shared/functions/kurin-label.function';

@Component({
  selector: 'app-waitlist-management',
  imports: [
    TableModule,
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

    <section class="card p-4 mb-6 branch-requests" aria-labelledby="branch-requests-title">
      <h2 id="branch-requests-title" class="text-xl font-bold mb-1">Запити куренів</h2>
      <p class="m-0 mb-4 text-sm text-muted-color">Старші курені просять додати чи прибрати улад або змінити число в ньому.</p>
      @if (branchRequests.length) {
        <ul class="branch-requests__list">
          @for (request of branchRequests; track request.kurinBranchRequestKey) {
            <li class="branch-requests__item">
              <div class="branch-requests__body">
                <strong>{{ request.kurinNamedAfter || branchesName(request.currentBranches) }}</strong>
                @if (request.kurinNamedAfter) {
                  <small class="text-muted-color">{{ branchesName(request.currentBranches) }}</small>
                }
                <span class="branch-requests__change">
                  <i class="pi pi-arrow-right" aria-hidden="true"></i>
                  {{ branchesName(request.wantedBranches) }}
                </span>
                <small class="text-muted-color">
                  {{ request.requestedByName || 'Хтось із проводу' }} · {{ request.requestedAtUtc | localUtcDate:'short' }}
                </small>
                @if (request.note) {
                  <p class="m-0 text-sm">«{{ request.note }}»</p>
                }
              </div>
              <div class="flex gap-2">
                <p-button icon="pi pi-check" severity="success" rounded text [loading]="decidingKey === request.kurinBranchRequestKey"
                          (onClick)="decideBranchRequest(request, true)" pTooltip="Схвалити: курінь отримає ці улади й числа" ariaLabel="Схвалити запит" />
                <p-button icon="pi pi-times" severity="danger" rounded text [disabled]="decidingKey !== null"
                          (onClick)="decideBranchRequest(request, false)" pTooltip="Відхилити" ariaLabel="Відхилити запит" />
              </div>
            </li>
          }
        </ul>
      } @else {
        <p class="m-0 text-sm text-muted-color">Запитів від куренів немає.</p>
      }
    </section>

    <div class="card p-4">
      <div class="flex justify-between items-center mb-6">
        <h2 class="text-2xl font-bold">Заявки на приєднання</h2>
        <p-selectButton
          [options]="sourceOptions"
          [(ngModel)]="sourceFilter"
          optionLabel="label"
          optionValue="value"
          [allowEmpty]="false"
          ariaLabel="Які записи показати" />
      </div>

      <p-table [value]="visibleEntries" [responsiveLayout]="'scroll'" [loading]="loading" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Імʼя</th>
            <th>Email</th>
            <th>Станиця</th>
            <th>Край</th>
            <th>Претендує на курінь</th>
            <th>Статус</th>
            <th>Подано</th>
            <th style="width: 120px">Дії</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-entry>
          <tr>
            <td>{{ entry.firstName }} {{ entry.lastName }}</td>
            <td>{{ entry.email }}</td>
            <td>{{ entry.stanytsia || '-' }}</td>
            <td>{{ kraiName(entry) }}</td>
            <td>
              @if (isDirectAdd(entry)) {
                <span class="text-gray-400 text-sm italic">Доданий проводом</span>
              } @else if (entry.isSeniorKurin) {
                <p-tag severity="info" [value]="seniorClaim(entry)" pTooltip="Старший курінь: один курінь з числом у кожному уладі" />
              } @else if (entry.isPreparatoryKurin) {
                <p-tag severity="warn" value="Підготовчий курінь" pTooltip="Курінь ще без числа: його задають у налаштуваннях куреня" />
              } @else if (entry.isKurinLeaderCandidate) {
                <p-tag severity="info" [value]="youthClaim(entry)" />
              } @else {
                <span class="text-gray-400 text-sm italic">Звичайний учасник</span>
              }
            </td>
            <td>
              <p-tag [severity]="getStatusSeverity(entry.verificationStatus)" [value]="getStatusLabel(entry.verificationStatus)" />
            </td>
            <td>{{ entry.requestedAtUtc | localUtcDate:'short' }}</td>
            <td>
              <div class="flex gap-2">
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
              </div>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
            <tr>
                <td colspan="8" class="lil-empty-cell">
                    <app-empty-state
                        art="list"
                        [title]="sourceFilter === 'DirectAdd' ? 'Нікого не додавали напряму' : 'Заявок немає'"
                        [body]="sourceFilter === 'DirectAdd'
                          ? 'Тут зʼявляться ті, кому провід відкрив акаунт прямо з куреня.'
                          : 'Нові заявки на приєднання зʼявляться тут.'" />
                </td>
            </tr>
        </ng-template>
      </p-table>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .branch-requests__list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .branch-requests__item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
      padding: 0.75rem 1rem;
      border: 1px solid var(--p-content-border-color);
      border-radius: 12px;
    }
    .branch-requests__body {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      min-width: 0;
    }
    .branch-requests__change {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
    }
    .waitlist-empty-cell {
      border: 0 !important;
      padding: 1.25rem 0 0 !important;
    }
  `]
})
export class WaitlistManagementComponent implements OnInit {
  entries: WaitlistEntry[] = [];
  loading = true;

  /**
   * Applications from зв'язкові by default. People a провід added straight into a kurin also get an
   * entry, for their invitation to hang off, but nobody has to decide on them, so they wait apart.
   */
  sourceFilter: WaitlistEntrySource | 'All' = 'Application';
  readonly sourceOptions: { label: string; value: WaitlistEntrySource | 'All' }[] = [
    { label: 'Заявки', value: 'Application' },
    { label: 'Додані напряму', value: 'DirectAdd' },
    { label: 'Усі', value: 'All' }
  ];

  get visibleEntries(): WaitlistEntry[] {
    if (this.sourceFilter === 'All') {
      return this.entries;
    }
    return this.entries.filter(entry => this.isDirectAdd(entry) === (this.sourceFilter === 'DirectAdd'));
  }

  isDirectAdd(entry: WaitlistEntry): boolean {
    return entry.source === 'DirectAdd';
  }

  rejectionDialogVisible = false;
  rejectionNote = '';
  selectedEntry: WaitlistEntry | null = null;

  private readonly onboardingService = inject(OnboardingService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly authService = inject(AuthService);
  private readonly waitlistAttention = inject(WaitlistAttentionService);

  private readonly branchRequestService = inject(KurinBranchRequestService);
  branchRequests: KurinBranchRequestDto[] = [];
  decidingKey: string | null = null;

  krais: KraiOption[] = [];
  approvalDialogVisible = false;
  approvalEntry: WaitlistEntry | null = null;
  approvalKrai: string | null = null;
  approving = false;

  ngOnInit() {
    this.loadEntries();
    this.loadBranchRequests();
    this.onboardingService.getKrais().subscribe({
      next: krais => this.krais = krais,
      // Without the list the column shows what the applicant wrote, and approval keeps their край.
      error: () => undefined
    });
  }

  kraiName(entry: WaitlistEntry): string {
    return this.krais.find(krai => krai.code === entry.kraiCode)?.name
      ?? entry.kraiCode
      ?? entry.regionOrCountry
      ?? '-';
  }

  loadEntries() {
    this.loading = true;
    this.onboardingService.getWaitlistEntries().subscribe({
      next: (data) => {
        this.entries = data;
        this.loading = false;
        this.waitlistAttention.countFrom(data);
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Помилка', detail: 'Не вдалося завантажити заявки' });
        this.loading = false;
      }
    });
  }

  loadBranchRequests(): void {
    this.branchRequestService.getPending().subscribe({
      next: requests => {
        this.branchRequests = requests;
        this.waitlistAttention.countRequests(requests.length);
      },
      error: () => undefined
    });
  }

  branchesName(branches: KurinNumbering[]): string {
    return kurinBranchesName(branches);
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
    if (!entry) return;

    const kraiCode = entry.isKurinLeaderCandidate ? this.approvalKrai : null;
    this.approving = true;
    this.onboardingService.approveWaitlistEntry(entry.waitlistEntryKey, kraiCode).subscribe({
        next: () => {
          this.approving = false;
          this.approvalDialogVisible = false;
          this.messageService.add({ severity: 'success', summary: 'Схвалено', detail: 'Запрошення надіслано' });
          this.loadEntries();
        },
        error: (err) => {
          this.approving = false;
          this.messageService.add({ severity: 'error', summary: 'Помилка', detail: err.error?.message || 'Не вдалося схвалити' });
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

  getStatusLabel(status: string | number): string {
    return getWaitlistStatusLabel(status);
  }

  getStatusSeverity(status: string | number): "success" | "info" | "warn" | "danger" | "secondary" | "contrast" | undefined {
    return getWaitlistStatusSeverity(status);
  }

  isInitial(status: string | number): boolean {
    return isWaitlistInitial(status);
  }

  isApproved(status: string | number): boolean {
    return isWaitlistApproved(status);
  }
}
