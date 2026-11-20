import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';

import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

// Optimus UI Imports
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { SelectModule } from '@openng/optimus-ui/select';
import { ButtonModule } from '@openng/optimus-ui/button';
import { PanelModule } from '@openng/optimus-ui/panel';
import { DividerModule } from '@openng/optimus-ui/divider';
import { FloatLabelModule } from '@openng/optimus-ui/floatlabel';

import { RoleWeight, RoleWeightOptions } from '../../models/enums/role-weight.enum';

import { PlanningService } from '../../services/planning-service/planning.service';
import { MemberService } from '../../services/member-service/member.service';
import { MemberLookupDto } from '../../models/requests/member/member-lookup.dto';


@Component({
  selector: 'app-create-planning',
  imports: [
    ReactiveFormsModule,
    InputTextModule,
    InputNumberModule,
    DatePickerModule,
    SelectModule,
    ButtonModule,
    PanelModule,
    DividerModule,
    FloatLabelModule
],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-center gap-4 mb-6">
        <p-button icon="pi pi-arrow-left" [text]="true" (click)="goBack()" />
        <h1 class="text-2xl font-bold">Нове планування</h1>
      </div>
    
      <form [formGroup]="form" (ngSubmit)="submit()">
    
        <p-panel header="Основні налаштування" styleClass="mb-6">
          <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
    
            <div class="flex flex-col gap-2">
              <label for="planning-name" class="font-semibold">Назва сесії</label>
              <input id="planning-name" pInputText formControlName="name" placeholder="Напр. Літо 2025" />
            </div>
    
            <div class="flex flex-col gap-2">
              <label for="planning-duration" class="font-semibold">Тривалість (днів)</label>
              <p-input-number inputId="planning-duration" formControlName="durationDays" [min]="1" [max]="30" [showButtons]="true" />
            </div>
    
            <div class="flex flex-col gap-2">
              <label for="planning-search-range" class="font-semibold">Вікно пошуку</label>
              <p-datepicker
                inputId="planning-search-range"
                formControlName="searchRange"
                selectionMode="range"
                [showIcon]="true"
                dateFormat="dd.mm.yy"
                placeholder="Вибери період" />
              </div>
            </div>
          </p-panel>
    
          <p-panel header="Учасники та зайнятість">
            <div formArrayName="participants" class="flex flex-col gap-4">
    
              @for (p of participantsArray.controls; track p; let i = $index) {
                <div [formGroupName]="i"
                  class="p-4 border border-surface rounded-lg bg-[var(--p-content-background)]">
                  <div class="flex flex-wrap md:flex-nowrap gap-4 items-start">
                    <div class="w-full md:w-1/4 flex flex-col gap-3">
                      <div class="font-bold text-lg text-color">
                        {{ p.get('fullName')?.value }}
                      </div>
                      <div class="flex flex-col gap-1">
                        <label [for]="'participant-role-weight-' + i" class="text-sm text-muted-color">Важливість голосу</label>
                        <p-select
                          [inputId]="'participant-role-weight-' + i"
                          formControlName="roleWeight"
                          [options]="weightOptions"
                          optionLabel="label"
                          optionValue="value"
                          class="w-full" />
                        </div>
                      </div>
                      <div class="hidden md:block w-px bg-[var(--p-content-border-color)] self-stretch mx-2"></div>
                      <div class="flex-1">
                        <div class="flex justify-between items-center mb-2">
                          <span class="text-sm font-semibold text-muted-color">
                            Коли ця людина зайнята?
                          </span>
                          <p-button
                            label="Додати період"
                            icon="pi pi-plus"
                            size="small"
                            [outlined]="true"
                            (click)="addBusyRange(i)" />
                          </div>
                          <div formArrayName="busyRanges" class="flex flex-col gap-2">
                            @for (range of getBusyRanges(i).controls; track range; let j = $index) {
                              <div [formGroupName]="j"
                                class="flex items-center gap-2">
                                <p-datepicker
                                  formControlName="range"
                                  selectionMode="range"
                                  dateFormat="dd.mm.yy"
                                  [readonlyInput]="true"
                                  placeholder="Вибери дати"
                                  appendTo="body"
                                  [style]="{'width':'240px'}" />
                                  <p-button
                                    icon="pi pi-trash"
                                    severity="danger"
                                    [text]="true"
                                    (click)="removeBusyRange(i, j)" />
                                  </div>
                                }
                                @if (getBusyRanges(i).length === 0) {
                                  <div class="text-sm text-muted-color italic py-2">
                                    Зазначте дати, якщо людина має плани...
                                  </div>
                                }
                              </div>
                            </div>
                          </div>
                        </div>
                      }
    
                    </div>
                  </p-panel>
    
                  <div class="flex justify-end gap-4 mt-6 pb-10">
                    <p-button label="Створити й розрахувати" icon="pi pi-cog" type="submit" [loading]="loading" />
                  </div>
                </form>
              </div>
    `
})
export class CreatePlanningComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly planningService = inject(PlanningService);
  private readonly memberService = inject(MemberService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  kurinKey = '';
  loading = false;
  weightOptions = RoleWeightOptions;

  form = this.fb.group({
    name: ['', Validators.required],
    kurinKey: ['', Validators.required],
    durationDays: [10, [Validators.required, Validators.min(1)]],
    searchRange: new FormControl<Date[] | null>(null, Validators.required),
    participants: this.fb.array([])
  });

  get participantsArray() {
    return this.form.get('participants') as FormArray;
  }

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      this.kurinKey = params.get('kurinKey')!;
      if (this.kurinKey) {
        this.form.patchValue({ kurinKey: this.kurinKey });
        this.setDefaultDates();
        this.loadMembers();
      }
    });
  }

  setDefaultDates() {
    const currentYear = new Date().getFullYear();
    const start = new Date(currentYear, 5, 1); // Червень (місяці з 0)
    const end = new Date(currentYear, 7, 31);  // Серпень
    
    this.form.get('searchRange')?.setValue([start, end]);
    this.form.get('name')?.setValue(`Табір ${currentYear}`);
  }

  loadMembers() {
    this.loading = true;
    this.memberService.getKVMembers(this.kurinKey).subscribe({
      next: (members) => {
        members.forEach(m => {
          this.participantsArray.push(this.createParticipantGroup(m));
        });
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  createParticipantGroup(member: MemberLookupDto): FormGroup {
    return this.fb.group({
      memberKey: [member.memberKey], // ID
      fullName: [`${member.firstName} ${member.lastName}`],
      roleWeight: [RoleWeight.Medium],
      busyRanges: this.fb.array([])
    });
  }

  // --- Date Range Logic ---

  getBusyRanges(participantIndex: number): FormArray {
    return this.participantsArray.at(participantIndex).get('busyRanges') as FormArray;
  }

  addBusyRange(participantIndex: number) {
    const rangeGroup = this.fb.group({
      range: [null, Validators.required]
    });
    this.getBusyRanges(participantIndex).push(rangeGroup);
  }

  removeBusyRange(pIndex: number, rIndex: number) {
    this.getBusyRanges(pIndex).removeAt(rIndex);
  }

  // --- Submit ---

  submit() {
    if (this.form.invalid) return;

    this.loading = true;
    const formVal = this.form.value;
    const participants = (formVal.participants ?? []) as {
      memberKey: string;
      fullName: string;
      roleWeight: RoleWeight;
      busyRanges?: { range: Date[] | null }[];
    }[];

    const payload = {
      name: formVal.name,
      kurinKey: formVal.kurinKey,
      durationDays: formVal.durationDays,
      searchStart: formVal.searchRange![0],
      searchEnd: formVal.searchRange![1],
      participants: participants.map((participant) => ({
        memberKey: participant.memberKey,
        fullName: participant.fullName,
        roleWeight: participant.roleWeight,
        busyRanges: (participant.busyRanges ?? []).map((rangeItem) => ({
          start: rangeItem.range?.[0],
          end: rangeItem.range?.[1]
        })).filter((rangeItem): rangeItem is { start: Date; end: Date } => Boolean(rangeItem.start && rangeItem.end))
      }))
    };

    this.planningService.createSession(payload).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/planning', this.kurinKey]);
      },
      error: () => this.loading = false
    });
  }

  goBack() {
    this.router.navigate(['/planning', this.kurinKey]);
  }
}