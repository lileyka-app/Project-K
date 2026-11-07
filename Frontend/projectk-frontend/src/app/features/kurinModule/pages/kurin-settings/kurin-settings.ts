import { Component, inject, OnInit, ChangeDetectionStrategy, viewChild } from '@angular/core';
import { forkJoin, of } from 'rxjs';
import { MessageService } from '@openng/optimus-ui/api';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MessageModule } from '@openng/optimus-ui/message';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { TagModule } from '@openng/optimus-ui/tag';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { HttpErrorResponse } from '@angular/common/http';
import { KurinNumberRange, OnboardingService } from '../../../authModule/services/onboarding-service/onboarding.service';
import { KurinDto } from '../../models/kurin.dto';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { AgendaCategoryManagerComponent } from '../../components/agenda-category-manager/agenda-category-manager';
import { AgendaArchivePolicyComponent } from '../../components/agenda-archive-policy/agenda-archive-policy';
import { KurinCardFieldsComponent } from '../../components/kurin-card-fields/kurin-card-fields';
import { KurinAccessPolicyComponent } from '../../components/kurin-access-policy/kurin-access-policy';
import { KurinCardSharingComponent } from '../../components/kurin-card-sharing/kurin-card-sharing';
import { KurinProbeEditionsComponent } from '../../components/kurin-probe-editions/kurin-probe-editions';
import { KurinTagSettingsComponent } from '../../components/kurin-tag-settings/kurin-tag-settings';
import { hasYouthProgram, isSeniorBranch } from '../../models/enums/kurin-branch.enum';
import { KurinBranchRequestComponent } from '../../components/kurin-branch-request/kurin-branch-request';
import { kurinBranchesName } from '../../../../shared/functions/kurin-label.function';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { LeadershipRole } from '../../models/enums/leadership-role.enum';
import { ROLE_DISPLAY_NAMES } from '../../models/role-display-name.model';
import { rememberKurinRoleNames } from '../../functions/leadership-role-display.function';

/** Ролі, які курінь може назвати по-своєму (сервер: KurinRoleNames.Renamable). Гурткових тут немає. */
const RENAMABLE_ROLES: LeadershipRole[] = [
  LeadershipRole.Zvyazkovyi, LeadershipRole.Kurinnuy, LeadershipRole.Pysar, LeadershipRole.Skarbnyk,
  LeadershipRole.Suddya, LeadershipRole.Horunjiy, LeadershipRole.Gospodar, LeadershipRole.Hronikar,
  LeadershipRole.Instruktor, LeadershipRole.Vykhovnyk, LeadershipRole.OtherKurin
];

@Component({
  selector: 'app-kurin-settings',
  imports: [
    FormsModule,
    ButtonModule,
    MessageModule,
    ToggleSwitchModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
    TagModule,
    AgendaCategoryManagerComponent,
    AgendaArchivePolicyComponent,
    KurinCardFieldsComponent,
    KurinAccessPolicyComponent,
    KurinProbeEditionsComponent,
    KurinTagSettingsComponent,
    KurinBranchRequestComponent,
    KurinCardSharingComponent
  ],
  templateUrl: './kurin-settings.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './kurin-settings.css'
})
export class KurinSettingsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly kurinService = inject(KurinService);
  private readonly onboardingService = inject(OnboardingService);
  private readonly messages = inject(MessageService);
  /** The archive terms are part of the page and go out with its one «Зберегти». */
  private readonly archivePolicy = viewChild<AgendaArchivePolicyComponent>('archivePolicy');
  private readonly cardFields = viewChild<KurinCardFieldsComponent>('cardFields');
  private readonly accessPolicy = viewChild<KurinAccessPolicyComponent>('accessPolicy');
  private readonly probeEditions = viewChild<KurinProbeEditionsComponent>('probeEditions');
  private readonly tagSettings = viewChild<KurinTagSettingsComponent>('tagSettings');
  private readonly cardSharing = viewChild<KurinCardSharingComponent>('cardSharing');

  /** Проби є лише в юнацькому курені; в УСП чи УПС розділу проби гуртків немає. */
  protected readonly hasYouthProgram = hasYouthProgram;

  /** Рядки «Назви ролей»: звична назва і те, як її називає курінь (порожнє — звична). */
  protected readonly renamableRoles = RENAMABLE_ROLES.map(role => ({ role, usual: ROLE_DISPLAY_NAMES[role] }));
  protected roleNames: Record<string, string> = {};

  private trimmedRoleNames(): Record<string, string> {
    return Object.fromEntries(Object.entries(this.roleNames).map(([role, name]) => [role, name.trim()]).filter(([, name]) => name));
  }

  /** A senior kurin's numbers are the admin's to change, so its settings ask instead of edit. */
  get isSenior(): boolean {
    return isSeniorBranch(this.kurin?.branch);
  }

  get title(): string {
    if (!this.kurin) return '-';
    if (this.isSenior) return this.kurin.namedAfter || kurinBranchesName(this.kurin.branches);
    return `${this.kurin.number ?? 'Підготовчий'} курінь`;
  }

  /** The КВ reads the page whole; only who may change the kurin edits it. */
  readonly readOnly = !inject(PermissionService).canManageKurinSettings();

  kurinKey = '';
  kurin: KurinDto | null = null;
  profileVerificationEnabled = false;
  /** Точкування в цьому курені; вимкнене зникає з меню, плиток і сторінок, а бали лишаються. */
  scoreEnabled = true;
  /** A підготовчий курінь has no number; clearing the box asks for one. */
  preparatory = false;
  number: number | null = null;
  /** The server says the real range; this is its default until it answers. */
  numberRange: KurinNumberRange = { min: 1, max: 200 };
  loading = true;
  saving = false;
  errorMessage: string | null = null;

  ngOnInit(): void {
    this.kurinKey = this.route.snapshot.paramMap.get('kurinKey') ?? '';

    if (!this.kurinKey) {
      this.errorMessage = 'Не вдалося визначити курінь для налаштувань.';
      this.loading = false;
      return;
    }

    this.loadSettings();
    this.onboardingService.getKurinNumberRange().subscribe({
      next: range => this.numberRange = range,
      // The default range stands in; the server still checks the real one.
      error: () => undefined
    });
  }

  /** A kurin may keep the number it has, even one outside the range (the team's kurin 0). */
  get keepsOwnNumber(): boolean {
    return this.number !== null && this.number === this.kurin?.number;
  }

  get numberProblem(): string | null {
    if (this.preparatory || this.loading) return null;
    if (this.number === null) return 'Впишіть число куреня або позначте його підготовчим.';
    if (!this.keepsOwnNumber && (this.number < this.numberRange.min || this.number > this.numberRange.max)) {
      return `Число куреня від ${this.numberRange.min} до ${this.numberRange.max}.`;
    }
    return null;
  }

  loadSettings(): void {
    this.loading = true;
    this.errorMessage = null;

    this.kurinService.getByKey(this.kurinKey).subscribe({
      next: (kurin) => {
        this.kurin = kurin;
        this.profileVerificationEnabled = kurin.profileVerificationEnabled ?? false;
        this.scoreEnabled = kurin.scoreEnabled ?? true;
        this.roleNames = { ...(kurin.roleNames ?? {}) };
        this.preparatory = kurin.number === null;
        this.number = kurin.number;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading kurin settings:', error);
        this.errorMessage = 'Не вдалося завантажити налаштування куреня.';
        this.loading = false;
      }
    });
  }

  save(): void {
    if (!this.kurin || this.saving || this.numberProblem) return;

    const archive = this.archivePolicy();
    if (archive && !archive.pending()) {
      this.errorMessage = 'Строк архіву — від 1 до 3650 днів.';
      return;
    }

    const request: KurinDto = {
      ...this.kurin,
      roleNames: this.kurin?.canRenameRoles ? this.trimmedRoleNames() : this.kurin?.roleNames,
      number: this.preparatory ? null : this.number,
      profileVerificationEnabled: this.profileVerificationEnabled,
      scoreEnabled: this.scoreEnabled
    };

    this.saving = true;
    this.errorMessage = null;

    const cardFields = this.cardFields();
    const accessPolicy = this.accessPolicy();
    const probeEditions = this.probeEditions();
    const tagSettings = this.tagSettings();
    const cardSharing = this.cardSharing();
    forkJoin([
      this.kurinService.updateKurin(request),
      archive ? archive.save() : of(undefined),
      cardFields ? cardFields.save() : of(undefined),
      accessPolicy ? accessPolicy.save() : of(undefined),
      probeEditions ? probeEditions.save() : of(undefined),
      tagSettings ? tagSettings.save() : of(undefined),
      cardSharing ? cardSharing.save() : of(undefined)
    ]).subscribe({
      next: ([updated]) => {
        this.kurin = updated;
        this.profileVerificationEnabled = updated.profileVerificationEnabled ?? false;
        this.scoreEnabled = updated.scoreEnabled ?? true;
        this.roleNames = { ...(updated.roleNames ?? {}) };
        rememberKurinRoleNames(updated.kurinKey, updated.roleNames);
        this.preparatory = updated.number === null;
        this.number = updated.number;
        this.saving = false;
        this.messages.add({ severity: 'success', summary: 'Збережено' });
      },
      error: (error: HttpErrorResponse) => {
        console.error('Error saving kurin settings:', error);
        this.errorMessage = this.saveFailure(error.error?.error);
        this.saving = false;
      }
    });
  }

  private saveFailure(code: string | undefined): string {
    switch (code) {
      case 'KurinNumberTaken':
        return `Курінь з числом ${this.number} уже є в Лілейці. Перевірте число.`;
      case 'KurinNumberOutOfRange':
        return `Число куреня від ${this.numberRange.min} до ${this.numberRange.max}.`;
      case 'ProbeEditionNotOffered':
        return 'Пробу гуртка не збережено: такої проби для вашого краю вже немає. Онови сторінку.';
      case 'AccessPolicyInvalid':
        return 'Права й видимість не збережено: сервер не прийняв цю таблицю. Онови сторінку й спробуй ще раз.';
      default:
        return 'Не вдалося зберегти налаштування куреня.';
    }
  }

  cancel(): void {
    this.router.navigate(['/kurin']);
  }
}
