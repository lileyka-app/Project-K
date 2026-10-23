import { Component, inject, signal, OnChanges, SimpleChanges, ChangeDetectionStrategy, input, model } from '@angular/core';
import { DatePipe } from '@angular/common';
import { PlanningService } from '../../services/planning-service/planning.service';
import { AgendaService } from '../../services/agenda-service/agenda.service';
import { PlanningSessionDto } from '../../models/planning-session.dto';

// Optimus UI Imports
import { DialogModule } from '@openng/optimus-ui/dialog';
import { ChartModule } from '@openng/optimus-ui/chart';
import { SkeletonModule } from '@openng/optimus-ui/skeleton';
import { ButtonModule } from '@openng/optimus-ui/button';
import { TagModule } from '@openng/optimus-ui/tag';
import { DividerModule } from '@openng/optimus-ui/divider';
import { MessageService } from '@openng/optimus-ui/api';

import 'chartjs-adapter-date-fns';

@Component({
  selector: 'app-planning-detail',
  imports: [
    DialogModule,
    ChartModule,
    SkeletonModule,
    ButtonModule,
    TagModule,
    DividerModule,
    DatePipe
],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
<p-dialog
  [(visible)]="visible"
  [style]="{ width: '95vw', maxWidth: '1200px' }"
  [header]="'Деталі: ' + (session()?.name || '...')"
  [modal]="true"
  [draggable]="false"
  [resizable]="false"
  [dismissableMask]="true"
  appendTo="body"
  (onHide)="close()">

  <ng-template pTemplate="content">

    @if (loading()) {
      <div class="flex flex-col gap-4">
        <p-skeleton height="100px" width="100%" />
        <p-skeleton height="300px" width="100%" />
      </div>
    } @else {
      @if (session(); as s) {
        <div class="flex flex-col gap-6">
          <div class="bg-[var(--p-content-background)] p-4 rounded-xl border border-surface flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
            <div>
              <div class="text-xs text-muted-color font-bold uppercase tracking-wider">Період пошуку</div>
              <div class="font-bold text-lg text-color">
                {{ s.searchStart | date:'dd.MM.yyyy' }} — {{ s.searchEnd | date:'dd.MM.yyyy' }}
              </div>
            </div>
            @if (s.isCalculated) {
              <div class="w-full md:w-auto flex items-center gap-3 bg-[var(--p-content-background)] px-4 py-2 rounded-lg border border-[var(--p-primary-color)]">
                <i class="pi pi-check-circle text-2xl text-[var(--p-primary-color)] shrink-0"></i>
                <div>
                  <div class="text-[10px] text-[var(--p-primary-color)] font-bold uppercase">Оптимальна дата</div>
                  <div class="text-xl font-bold leading-none mt-1">
                    {{ s.optimalStartDate | date:'dd.MM' }} — {{ s.optimalEndDate | date:'dd.MM' }}
                  </div>
                </div>
              </div>
            }
          </div>
          <div class="border border-surface rounded-xl p-2 md:p-4 overflow-hidden bg-[var(--p-content-background)]">
            <h3 class="font-bold text-color mb-4 ml-2">Графік зайнятості</h3>
            @if (chartData) {
              <div class="relative w-full">
                <p-chart type="bar"
                  [data]="chartData"
                  [options]="chartOptions"
                  [height]="calculateHeight()"
                  [responsive]="true" />
              </div>
            }
            @if (!chartData) {
              <div class="text-center p-4 text-muted-color">
                Немає даних для відображення
              </div>
            }
          </div>
        </div>
      }
    }

  </ng-template> <ng-template pTemplate="footer">
  @if (session()?.isCalculated && session()?.optimalStartDate) {
    <p-button label="Перенести в календар" icon="pi pi-calendar-plus" (click)="addToCalendar()" [disabled]="adding()" />
  }
  <p-button label="Закрити" (click)="close()" [text]="true" severity="secondary" />
</ng-template>
</p-dialog>
`
})
export class PlanningDetailComponent implements OnChanges {
  readonly visible = model(false);
  readonly sessionId = input<string | null>(null);

  private readonly service = inject(PlanningService);
  private readonly agendaService = inject(AgendaService);
  private readonly messages = inject(MessageService);

  session = signal<PlanningSessionDto | null>(null);
  loading = signal(false);
  adding = signal(false);

  // Дані для Chart.js
  chartData: {
    labels: string[];
    datasets: {
      label: string;
      backgroundColor: string;
      borderColor: string;
      borderWidth: number;
      barPercentage: number;
      data: { x: [string, string]; y: string }[];
    }[];
  } | null = null;
  chartOptions: Record<string, unknown> | null = null;

  ngOnChanges(changes: SimpleChanges) {
    const sessionId = this.sessionId();
    if (changes['visible'] && this.visible() && sessionId) {
      this.loadData(sessionId);
    }
  }

  loadData(id: string) {
    this.loading.set(true);
    this.service.getSessionByKey(id).subscribe({
      next: (data) => {
        this.session.set(data);
        this.initChart(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  close() {
    this.visible.set(false);
  }

  /** Turn this calculated planning into a kurin-wide calendar event (name + optimal dates). */
  addToCalendar() {
    const s = this.session();
    if (!s?.optimalStartDate) {
      return;
    }
    this.adding.set(true);
    this.agendaService.create({
      kurinKey: s.kurinKey,
      kind: 'Event',
      title: s.name,
      description: 'Створено з планування табору',
      location: null,
      startUtc: s.optimalStartDate,
      endUtc: s.optimalEndDate ?? null,
      isAllDay: true,
      agendaCategoryKey: null,
      recurrenceFrequency: 'None',
      recurrenceInterval: 1,
      recurrenceByWeekday: 0,
      recurrenceEndUtc: null,
      recurrenceCount: null,
      targets: [{ targetType: 'Kurin', targetKey: s.kurinKey }]
    }).subscribe({
      next: () => {
        this.adding.set(false);
        this.messages.add({ severity: 'success', summary: 'Додано в календар куреня' });
      },
      error: () => {
        this.adding.set(false);
        this.messages.add({ severity: 'error', summary: 'Не вдалося додати в календар' });
      }
    });
  }

  calculateHeight() {

    const count = this.session()?.participants.length || 5;
    return `${Math.max(300, count * 50 + 100)}px`;
  }

  initChart(s: PlanningSessionDto) {
    const documentStyle = getComputedStyle(document.documentElement);
    const textColor = documentStyle.getPropertyValue('--p-text-color').trim();
    const textColorSecondary = documentStyle.getPropertyValue('--p-text-muted-color').trim();
    const surfaceBorder = documentStyle.getPropertyValue('--p-content-border-color').trim();
    const optimalFill = documentStyle.getPropertyValue('--p-primary-color').trim();
    const optimalBorder = documentStyle.getPropertyValue('--p-primary-600').trim();
    const busyFill = documentStyle.getPropertyValue('--p-red-400').trim();
    const busyBorder = documentStyle.getPropertyValue('--p-red-600').trim();

    const labels = ['Табір', ...s.participants.map(p => p.fullName)];

    const optimalData: { x: [string, string]; y: string }[] = [];
    if (s.isCalculated && s.optimalStartDate && s.optimalEndDate) {
      optimalData.push({
        x: [s.optimalStartDate, s.optimalEndDate],
        y: labels[0]
      });
    }

    const busyData: { x: [string, string]; y: string }[] = [];
    s.participants.forEach(p => {
      p.busyRanges.forEach(range => {
        busyData.push({
          x: [range.start, range.end],
          y: p.fullName
        });
      });
    });

    this.chartData = {
      labels: labels,
      datasets: [
        {
          label: 'Оптимальний час',
          backgroundColor: optimalFill,
          borderColor: optimalBorder,
          borderWidth: 1,
          barPercentage: 0.6,
          data: optimalData
        },
        {
          label: 'Зайнятий',
          backgroundColor: busyFill,
          borderColor: busyBorder,
          borderWidth: 1,
          barPercentage: 0.5,
          data: busyData
        }
      ]
    };

    this.chartOptions = {
      indexAxis: 'y',
      maintainAspectRatio: false,
      aspectRatio: 0.8,
      plugins: {
        legend: {
          labels: { color: textColor }
        },
        tooltip: {
          callbacks: {
            label: (context: { raw: { x: [string, string] }; dataset: { label: string } }) => {
              const start = new Date(context.raw.x[0]).toLocaleDateString('uk-UA', {day: '2-digit', month: '2-digit'});
              const end = new Date(context.raw.x[1]).toLocaleDateString('uk-UA', {day: '2-digit', month: '2-digit'});
              return `${context.dataset.label}: ${start} - ${end}`;
            }
          }
        }
      },
      scales: {
        x: {
          type: 'time',
          time: {
            unit: 'day',
            tooltipFormat: 'dd.MM.yyyy',
            displayFormats: {
              day: 'dd.MM'
            }
          },
          min: s.searchStart,
          max: s.searchEnd,
          ticks: {
            color: textColorSecondary
          },
          grid: {
            color: surfaceBorder,
            drawBorder: false
          }
        },
        y: {
          ticks: {
            color: textColor,
            font: {
              weight: 'bold'
            }
          },
          grid: {
            color: surfaceBorder,
            drawBorder: false
          }
        }
      }
    };
  }
}