import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { TableModule } from '@openng/optimus-ui/table';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TagModule } from '@openng/optimus-ui/tag';
import { TooltipModule } from '@openng/optimus-ui/tooltip';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { ConfirmationService } from '@openng/optimus-ui/api';
import { PlanningService } from '../../services/planning-service/planning.service';
import { MemberService } from '../../services/member-service/member.service';
import { PlanningSessionDto } from '../../models/planning-session.dto';
import { PlanningDetailComponent } from '../../components/planning-detail/planning-detail';
import { PermissionService } from '../../../authModule/services/permission-service/permission.service';
import { EmptyStateComponent } from '../../../../shared/empty-state/empty-state';
import { PageTab, PageTabsComponent } from '../../../../shared/page-tabs/page-tabs';
import { PageGroups } from '../../services/page-groups/page-groups.service';

@Component({
  selector: 'app-planning-list',
  imports: [PageTabsComponent, TableModule, ButtonModule, TagModule, TooltipModule, ConfirmDialogModule, RouterModule, PlanningDetailComponent, EmptyStateComponent, DatePipe],
  providers: [ConfirmationService],
  template: `
    <p-confirmdialog />
    <div class="lil-page">
      <header class="lil-page-head">
        <div>
          <h1>Планування</h1>
        </div>
        @if (canCreatePlanning && sessions().length) {
          <div class="lil-page-head__actions">
            <p-button label="Нове планування" icon="pi pi-plus" (click)="createNew()" />
          </div>
        }
      </header>

      <app-page-tabs [tabs]="tabs" label="Календар і планування" />

      <section class="kurin-tile">

        <p-table [value]="sessions()" styleClass="project-table planning-table">
          <ng-template pTemplate="header">
            <tr>
              <th scope="col">Назва</th>
              <th scope="col">Період пошуку</th>
              <th scope="col">Статус</th>
              <th scope="col" class="planning-actions-col">Дії</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-session>
            <tr class="planning-row">
              <td class="planning-name" data-mobile-label="Назва">{{ session.name }}</td>
              <td data-mobile-label="Період">
                {{ session.searchStart | date:'dd.MM' }} - {{ session.searchEnd | date:'dd.MM.yyyy' }}
              </td>
              <td data-mobile-label="Статус">
                <p-tag
                  [severity]="session.isCalculated ? 'success' : 'warn'"
                  [value]="session.isCalculated ? 'Розраховано' : 'Нове'" />
              </td>
              <td class="planning-actions-col" data-mobile-label="Дії">
                <div class="planning-actions">
                  <p-button icon="pi pi-eye" severity="secondary" [rounded]="true" [text]="true" (click)="openDetails(session.planningSessionKey)" pTooltip="Переглянути графік" ariaLabel="Переглянути графік"/>
                  @if (session.canDelete) {
                    <p-button icon="pi pi-trash" severity="danger" [rounded]="true" [text]="true" (click)="delete(session)" pTooltip="Видалити планування" ariaLabel="Видалити планування" />
                  }
                </div>
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="4" class="lil-empty-cell">
                <app-empty-state
                  art="calendar"
                  title="Календар ще спить"
                  body="Перша сходина в плані — і він прокинеться.">
                  @if (canCreatePlanning) {
                    <p-button label="Створити планування" icon="pi pi-plus" (click)="createNew()" />
                  }
                </app-empty-state>
              </td>
            </tr>
          </ng-template>
        </p-table>
      </section>

      @if (detailsVisible) {
        <app-planning-detail [(visible)]="detailsVisible"
          [sessionId]="selectedSessionId" />
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .planning-name {
      font-weight: 650;
    }

    .planning-actions-col {
      text-align: right;
      width: 6rem;
    }

    .planning-actions {
      display: flex;
      gap: 0.125rem;
      justify-content: flex-end;
    }

    :host ::ng-deep .planning-table .p-datatable-table {
      width: 100% !important;
      min-width: 0 !important;
    }

    @media (max-width: 640px) {
      :host ::ng-deep .planning-table .p-datatable-wrapper {
        overflow-x: visible;
      }

      :host ::ng-deep .planning-table .p-datatable-thead {
        display: none;
      }

      :host ::ng-deep .planning-table .p-datatable-tbody > tr.planning-row {
        border-bottom: 1px solid var(--p-content-border-color);
        display: grid;
        gap: 0.45rem;
        padding: 0.8rem 0;
      }

      :host ::ng-deep .planning-table .p-datatable-tbody > tr.planning-row > td {
        align-items: center;
        border: 0 !important;
        display: grid;
        gap: 0.6rem;
        grid-template-columns: 5.25rem minmax(0, 1fr);
        min-width: 0;
        overflow-wrap: anywhere;
        padding: 0.1rem 0 !important;
      }

      :host ::ng-deep .planning-table .p-datatable-tbody > tr.planning-row > td::before {
        color: var(--p-text-muted-color);
        content: attr(data-mobile-label);
        font-size: 0.75rem;
        font-weight: 650;
      }

      .planning-actions-col {
        text-align: left;
        width: auto;
      }

      .planning-actions {
        justify-content: flex-start;
      }
    }
  `]
})
export class PlanningListComponent implements OnInit {
  private readonly planningService = inject(PlanningService);
  private readonly memberService = inject(MemberService);
  private readonly permissionService = inject(PermissionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly confirmationService = inject(ConfirmationService);

  sessions = signal<PlanningSessionDto[]>([]);

  detailsVisible = false;
  selectedSessionId: string | null = null;

  kurinKey = '';
  private readonly pageGroups = inject(PageGroups);
  /** Календар і Планування — одне місце в меню, дві вкладки. */
  tabs: PageTab[] = [];

  get canCreatePlanning(): boolean {
    return this.permissionService.canCreatePlanning();
  }

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      this.kurinKey = params.get('kurinKey')!;
      this.tabs = this.pageGroups.calendar(this.kurinKey);
    });
    this.loadData();
  }

  loadData() {
    this.planningService.getSessions(this.kurinKey)
      .subscribe(data => {
        this.sessions.set(data);
      });
  }

  createNew() {
    this.router.navigate(['/planning/create', this.kurinKey]);
  }

  delete(session: PlanningSessionDto) {
    this.confirmationService.confirm({
      header: 'Видалити планування',
      message: `Планування «${session.name}» зникне разом із зайнятістю КВ. Повернути його не вийде; подія, яку з нього вже перенесли в календар, лишиться.`,
      icon: 'pi pi-trash',
      acceptLabel: 'Видалити',
      rejectLabel: 'Скасувати',
      acceptButtonProps: { label: 'Видалити', severity: 'danger' },
      rejectButtonProps: { label: 'Скасувати', severity: 'secondary', outlined: true },
      accept: () => this.planningService.deleteSession(session.planningSessionKey).subscribe(() => this.loadData())
    });
  }

  openDetails(id: string) {
    this.selectedSessionId = id;
    this.detailsVisible = true;
  }
}
