import { Component, OnChanges, inject, ChangeDetectionStrategy, output, input, model } from '@angular/core';

import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { SelectModule } from '@openng/optimus-ui/select';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { DatePickerModule } from '@openng/optimus-ui/datepicker';
import { MemberAwardDto } from '../../../../models/member-award.dto';
import { MemberAwardLevel } from '../../../../models/enums/member-award-level.enum';
import { BadgeProgressStatus } from '../../../../models/enums/badge-progress-status.enum';
import { UpsertMemberAwardRequest } from '../../../../services/member-award-service/member-award.service';
import { getBadgeProgressShortStatusLabel } from '../../../../functions/progress-status-labels.function';
import { parseDateOnlyString, toDateOnlyString } from '../../../../functions/to-date-only-string.function';

@Component({
  selector: 'app-member-awards-dialog',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    SelectModule,
    TextareaModule,
    DatePickerModule
],
  templateUrl: './member-awards-dialog.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './member-awards-dialog.css'
})
export class MemberAwardsDialogComponent implements OnChanges {
  readonly visible = model(false);
  readonly awardToEdit = input<MemberAwardDto | null>(null);
  readonly existingAwards = input<MemberAwardDto[]>([]);
  /** Change the level, date and note and save; a confirmed award only for whoever may remove one. */
  readonly canEdit = input(false);
  /** Confirm this award: the kurin lets the viewer confirm its level. */
  readonly canReview = input(false);
  readonly canDelete = input(false);

  readonly save = output<UpsertMemberAwardRequest>();
  readonly approve = output<string>();
  readonly delete = output<MemberAwardDto>();

  form: FormGroup;

  levels = [
    { label: 'Перша', value: MemberAwardLevel.First },
    { label: 'Друга', value: MemberAwardLevel.Second },
    { label: 'Третя', value: MemberAwardLevel.Third },
    { label: 'Четверта', value: MemberAwardLevel.Fourth }
  ];

  private readonly fb = inject(FormBuilder);

  constructor() {
    this.form = this.fb.group({
      level: [null, Validators.required],
      dateAcquired: [null, Validators.required],
      note: ['']
    });
  }

  ngOnChanges(): void {
    if (this.visible()) {
      if (this.canEdit()) {
        this.form.enable();
      } else {
        this.form.disable();
      }
      const awardToEdit = this.awardToEdit();
      if (awardToEdit) {
        this.form.patchValue({
          level: awardToEdit.level,
          dateAcquired: parseDateOnlyString(awardToEdit.dateAcquired),
          note: awardToEdit.note
        });
        this.form.get('level')?.disable();
      } else {
        this.form.reset();
        if (this.canEdit()) {
          this.form.get('level')?.enable();
        }
      }
    }
  }

  close(): void {
    this.visible.set(false);
  }

  onSave(): void {
    if (this.canEdit() && this.form.valid) {
      const formValue = this.form.getRawValue();
      const request: UpsertMemberAwardRequest = {
        memberAwardKey: this.awardToEdit()?.memberAwardKey,
        level: formValue.level,
        dateAcquired: toDateOnlyString(formValue.dateAcquired)!,
        note: formValue.note
      };
      this.save.emit(request);
      this.close();
    }
  }

  onApprove(): void {
    const awardToEdit = this.awardToEdit();
    if (!this.canApproveAward || !awardToEdit) {
      return;
    }

    this.approve.emit(awardToEdit.memberAwardKey);
    this.close();
  }

  onDelete(): void {
    const awardToEdit = this.awardToEdit();
    if (!this.canDeleteAward || !awardToEdit) {
      return;
    }

    this.delete.emit(awardToEdit);
    this.close();
  }

  get availableLevels() {
    return this.levels;
  }

  get canApproveAward(): boolean {
    const awardToEdit = this.awardToEdit();
    return this.canReview()
      && !!awardToEdit
      && this.normalizeStatus(awardToEdit.status) === BadgeProgressStatus.Submitted;
  }

  get canDeleteAward(): boolean {
    return this.canDelete() && !!this.awardToEdit();
  }

  get statusLabel(): string | null {
    const awardToEdit = this.awardToEdit();
    if (!awardToEdit) {
      return null;
    }
    return getBadgeProgressShortStatusLabel(this.normalizeStatus(awardToEdit.status));
  }

  private normalizeStatus(status: BadgeProgressStatus | string | number): BadgeProgressStatus {
    if (typeof status === 'number' && BadgeProgressStatus[status] !== undefined) {
      return status as BadgeProgressStatus;
    }

    if (typeof status === 'string') {
      const enumValue = BadgeProgressStatus[status as keyof typeof BadgeProgressStatus];
      if (typeof enumValue === 'number') {
        return enumValue;
      }
    }

    return BadgeProgressStatus.Draft;
  }
}
