import { WaitlistAttentionService } from '../../../adminModule/services/waitlist-attention/waitlist-attention.service';
import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { TableModule } from '@openng/optimus-ui/table';
import { SplitButtonModule } from '@openng/optimus-ui/splitbutton';

import { KurinDto } from '../../models/kurin.dto';
import { KraiDto } from '../../models/krai.dto';
import { of, switchMap } from 'rxjs';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { MenuItem, MessageService } from '@openng/optimus-ui/api';
import { ManageAction, ManagePanelComponent, ManagePanelConfig, ManagePanelField } from '../../components/manage-panel/manage-panel';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { failureDetail } from '../../../../shared/functions/failure-detail.function';
import { HttpErrorResponse } from '@angular/common/http';
import { isSeniorBranch, KURIN_BRANCH_LABELS, KurinBranch, KurinNumbering } from '../../models/enums/kurin-branch.enum';
import { PageTab, PageTabsComponent } from '../../../../shared/page-tabs/page-tabs';
import { TagModule } from '@openng/optimus-ui/tag';
import { compareKurinNumbers, compareKurinsByBranch, kurinBranchesName, kurinBranchTags, kurinGroupTitle } from '../../../../shared/functions/kurin-label.function';

/** The create form: the kurin, and optionally the person invited to be its Звʼязковий. */
type KurinCreateForm = KurinDto & {
  founderEmail?: string | null;
  founderFirstName?: string | null;
  founderLastName?: string | null;
};

@Component({
  selector: 'app-admin-panel',
  imports: [TableModule, TagModule, SplitButtonModule, ManagePanelComponent, ButtonModule, EmptyStateComponent, RouterModule, PageTabsComponent],
  templateUrl: './admin-panel.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './admin-panel.css'
})
export class AdminPanelComponent implements OnInit {

  private readonly router: Router = inject(Router);
  private readonly kurinService = inject(KurinService);
  private readonly authService = inject(AuthService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  readonly waitlistAttention = inject(WaitlistAttentionService);

  /** One улад at a time: a kurin in both senior ones is listed under each, by its number there. */
  readonly uladTabs: readonly PageTab[] = [
    { label: 'УПЮ', link: '/panel', queryParams: {}, testId: 'admin-ulad-upyu' },
    { label: 'УСП', link: '/panel', queryParams: { ulad: 'usp' }, testId: 'admin-ulad-usp' },
    { label: 'УПС', link: '/panel', queryParams: { ulad: 'ups' }, testId: 'admin-ulad-ups' }
  ];
  private static readonly uladsByParam: Readonly<Record<string, KurinBranch>> = { usp: KurinBranch.USP, ups: KurinBranch.UPS };
  ulad: KurinBranch = KurinBranch.UPYu;
  /** Every kurin in the system; `data` is the улад on screen. */
  kurins: KurinDto[] = [];

  selectedItem: KurinDto | null = null;
  managePanelVisible = false;
  managePanelParameter: 'create' | 'update' | 'delete' | 'undef' = 'undef';
  data: KurinDto[] = [];
  krais: KraiDto[] = [];

  actions: MenuItem[] = [];

  managePanelConfig: ManagePanelConfig = {
    entityType: 'kurin',
    title: 'Курінь',
    fields: [
      {
        name: 'kurinKey',
        label: 'Системний ключ',
        type: 'text',
        required: true,
        hiddenOn: ['create', 'delete'],
        disabledOn: ['update']
      },
      {
        name: 'number',
        label: 'Номер куреня',
        type: 'number',
        placeholder: 'Наприклад: 101',
        required: true,
        hiddenOn: ['delete'],
      },
      AdminPanelComponent.kraiField([]),
      {
        name: 'founderEmail',
        label: 'Email Звʼязкового',
        type: 'text',
        placeholder: 'Порожньо — курінь без Звʼязкового',
        hiddenOn: ['update', 'delete']
      },
      { name: 'founderFirstName', label: 'Імʼя Звʼязкового', type: 'text', hiddenOn: ['update', 'delete'] },
      { name: 'founderLastName', label: 'Прізвище Звʼязкового', type: 'text', hiddenOn: ['update', 'delete'] }
    ],
    displayName: (entity: KurinDto) => kurinGroupTitle(entity),
    createFactory: () => ({ kurinKey: '', number: null, kraiCode: null, founderEmail: null, founderFirstName: null, founderLastName: null }),
  }

  private static kraiField(krais: KraiDto[]): ManagePanelField {
    return {
      name: 'kraiCode',
      label: 'Край',
      type: 'select',
      placeholder: 'Типовий край інстансу',
      hiddenOn: ['delete'],
      options: krais.map(krai => ({ label: krai.name, value: krai.code }))
    };
  }

  prepareItemActions(item: KurinDto): void {
    this.actions = [
      {
        label: 'Редагувати',
        icon: 'pi pi-pencil',
        command: () => { this.onActionClick(item, 'update') }
      },
      {
        label: 'Видалити',
        icon: 'pi pi-trash',
        command: () => { this.onActionClick(item, 'delete') }
      }
    ];
  }

  ngOnInit(): void {
    this.route.queryParamMap.subscribe(params => {
      this.ulad = AdminPanelComponent.uladsByParam[params.get('ulad') ?? ''] ?? KurinBranch.UPYu;
      this.data = this.shownInUlad(this.kurins);
    });
    this.refreshData();
    this.kurinService.getKrais().subscribe((krais: KraiDto[]) => {
      this.krais = krais ?? [];
      this.data = this.shownInUlad(this.kurins);
      // A new config object, so the panel rebuilds its form with the choices in it.
      this.managePanelConfig = {
        ...this.managePanelConfig,
        fields: this.managePanelConfig.fields.map(field =>
          field.name === 'kraiCode' ? AdminPanelComponent.kraiField(this.krais) : field)
      };
    });
    this.waitlistAttention.refresh();
  }

  onActionClick(item: KurinDto | null, param: ManageAction | 'undef'): void {
    this.selectedItem = item;
    this.managePanelVisible = true;
    this.managePanelParameter = param;
  }

  onManageAction(e: { action: ManageAction; entity: KurinCreateForm; entityType: string }): void {
    switch (e.action) {
      case 'create':
        // With an address the kurin opens with an invitation for its Звʼязковий, who chooses their
        // own password on activation; without one it opens empty, to be claimed through the waitlist.
        if (e.entity.founderEmail?.trim()) {
          this.inviteFounder(e.entity);
        } else {
          this.kurinService.createKurin(e.entity).subscribe(() => { this.refreshData(); });
        }
        break;
      case 'update': {
        // The край moves through its own admin-only call, after the rest of the record is saved.
        const previousKrai = this.selectedItem?.kraiCode;
        const nextKrai = e.entity.kraiCode;
        this.kurinService.updateKurin(e.entity).pipe(
          switchMap(() => nextKrai && nextKrai !== previousKrai
            ? this.kurinService.setKurinKrai(e.entity.kurinKey, nextKrai)
            : of(null))
        ).subscribe({
          next: () => this.refreshData(),
          error: (error: unknown) => {
            this.refreshData();
            this.messageService.add({
              severity: 'error',
              summary: 'Не вдалося зберегти курінь',
              detail: failureDetail(error)
            });
          }
        });
        break;
      }
      case 'delete':
        this.kurinService.deleteKurin(e.entity.kurinKey).subscribe(() => this.refreshData());
        break;
    }
  }

  private inviteFounder(form: KurinCreateForm): void {
    const email = form.founderEmail!.trim();
    const firstName = form.founderFirstName?.trim();
    const lastName = form.founderLastName?.trim();
    if (!firstName || !lastName) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Курінь не створено',
        detail: 'Для запрошення потрібні імʼя й прізвище Звʼязкового.'
      });
      return;
    }

    this.kurinService.inviteKurinFounder({
      email,
      firstName,
      lastName,
      kurinNumber: form.number,
      kraiCode: form.kraiCode || null
    }).subscribe({
      next: invitation => {
        this.refreshData();
        this.messageService.add(invitation.invitationSent
          ? { severity: 'success', summary: 'Курінь створено', detail: `Запрошення надіслано на ${invitation.email}.` }
          : {
            severity: 'warn',
            summary: 'Курінь створено, лист не пішов',
            detail: 'Звʼязковий може попросити його знову через «Забув пароль» на сторінці входу.'
          });
      },
      error: (error: unknown) => this.messageService.add({
        severity: 'error',
        summary: 'Курінь не створено',
        detail: AdminPanelComponent.inviteFailure(error)
      })
    });
  }

  private static inviteFailure(error: unknown): string {
    const code = error instanceof HttpErrorResponse ? (error.error as { error?: string } | null)?.error : undefined;
    switch (code) {
      case 'EmailTaken':
        return 'За цією адресою вже є акаунт.';
      case 'WaitlistPending':
        return 'Від цієї адреси вже є заявка — схвали її в «Заявках».';
      case 'KurinNumberTaken':
        return 'Курінь з таким числом у цьому краї вже є.';
      case 'VALIDATION_ERROR':
        return 'Перевір адресу, імʼя та число куреня.';
      default:
        return failureDetail(error);
    }
  }

  refreshData(): void {
    this.kurinService.getKurins().subscribe((data: KurinDto[]) => {
      this.kurins = data ?? [];
      this.data = this.shownInUlad(this.kurins);
    });
  }

  /**
   * The kurins of the улад on screen, grouped by край: Україна first, then the rest by name; inside a
   * край by the kurin's number in this улад, підготовчі last.
   */
  private shownInUlad(kurins: KurinDto[]): KurinDto[] {
    const rank = (code?: string) => code === 'ua' ? '' : this.kraiName(code);
    return kurins
      .filter(kurin => this.ulad === KurinBranch.UPYu ? !this.isSenior(kurin) : this.numberingHere(kurin).length > 0)
      .sort((a, b) =>
        rank(a.kraiCode).localeCompare(rank(b.kraiCode), 'uk')
        || compareKurinsByBranch(a, b)
        || compareKurinNumbers(this.numberingHere(a)[0]?.number, this.numberingHere(b)[0]?.number));
  }

  /** The kurin's number in the senior улад on screen; nothing when it is not in it. */
  private numberingHere(kurin: KurinDto): KurinNumbering[] {
    const all = kurin.branches?.length ? kurin.branches : [{ branch: kurin.branch ?? KurinBranch.UPYu, number: kurin.number ?? null }];
    return all.filter(numbering => numbering.branch === this.ulad);
  }

  get uladLabel(): string {
    return KURIN_BRANCH_LABELS[this.ulad];
  }

  get isSeniorUlad(): boolean {
    return isSeniorBranch(this.ulad);
  }

  kraiName(code?: string): string {
    return this.krais.find(krai => krai.code === code)?.name ?? code ?? '';
  }

  isSenior(kurin: KurinDto): boolean {
    return isSeniorBranch(kurin.branch);
  }

  /** «УСП · УПС»: the улади a senior kurin stands in, on its row. */
  branchTags(kurin: KurinDto): string {
    return kurinBranchTags(kurin.branches, kurin.branch);
  }

  /** «5-й курінь УСП» in the УСП tab, «20-й курінь УПС» in the УПС one. */
  seniorTitle(kurin: KurinDto): string {
    const here = this.numberingHere(kurin);
    return kurinBranchesName(here.length ? here : kurin.branches);
  }

  kurinCountInKrai(code?: string): number {
    return this.data.filter(kurin => kurin.kraiCode === code).length;
  }

  onOpenClick(kurinKey: string): void {
    this.authService.setKurinScope(kurinKey).subscribe({
      next: () => this.router.navigate(['/kurin']),
      error: (error: unknown) => this.messageService.add({
        severity: 'error',
        summary: 'Не вдалося відкрити курінь',
        detail: failureDetail(error)
      })
    });
  }
}
