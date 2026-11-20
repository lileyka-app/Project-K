/* eslint-disable @typescript-eslint/no-explicit-any */
import { Component, ElementRef, OnChanges, SimpleChanges, ChangeDetectionStrategy, viewChildren, model, input, output } from '@angular/core';
import { DialogModule } from '@openng/optimus-ui/dialog';
import { TitleCasePipe } from '@angular/common';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { SelectModule } from '@openng/optimus-ui/select';
import { ButtonModule } from '@openng/optimus-ui/button';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';

export type ManageAction = 'create' | 'update' | 'delete';
export type ManageFieldType = 'text' | 'number' | 'textarea' | 'select';

export interface ManageFieldOption {
  label: string;
  value: string;
}

export interface ManagePanelField {
  name: string;
  label: string;
  type: ManageFieldType;
  placeholder?: string;
  required?: boolean;
  disabledOn?: ManageAction[];
  hiddenOn?: ManageAction[];
  /** The choices of a <code>select</code> field. */
  options?: ManageFieldOption[];
}

export interface ManagePanelConfig {
  entityType: string;
  title?: string;
  fields: ManagePanelField[];
  createFactory?: () => any;
  displayName?: (entity: any) => string;
  mapOut?(raw: any): any;
  /** What the delete action is called and what follows it, where it is not a plain, final delete. */
  deleteAction?: { verb: string; question: string; note: string };
}

@Component({
  selector: 'app-manage-panel',
  imports: [DialogModule, InputTextModule, TextareaModule, SelectModule, ButtonModule, FormsModule, ReactiveFormsModule, TitleCasePipe],
  templateUrl: './manage-panel.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './manage-panel.css'
})
export class ManagePanelComponent implements OnChanges {
  readonly visible = model(false);
  readonly parameter = input<ManageAction | 'undef'>('undef');
  readonly entity = input<any>(null);
  readonly config = input.required<ManagePanelConfig>();

  readonly actionPerformed = output<{
    action: ManageAction;
    entity: any;
    entityType: string;
}>();

  readonly autoFields = viewChildren<ElementRef>('autoField');

  form: FormGroup = new FormGroup({});
  ready = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['config']) {
      this.buildForm();
    } else if (changes['entity']) {
      this.patchEntity();
    }
    const parameter = this.parameter();
    if (changes['parameter']) {
      this.applyStatePerAction();
      if (parameter === 'create') {
        this.resetControlsState();
      }
    }
    if (changes['visible'] && this.visible()) {
      if (parameter === 'create') {
        this.buildForm(); // новий чистий об'єкт
      } else {
        // інші дії: просто пропатчити актуальне entity
        this.patchEntity();
        this.applyStatePerAction();
      }
      this.resetControlsState();
    }
  }
  
  onDialogShow(): void {
    if(!this.isDeleteMode()) {
      this.focusFirstInput();
    }
  }

  private focusFirstInput(): void {
    const target = this.autoFields()?.find(ref => {
      const el = ref.nativeElement as HTMLElement;
      const disabled = (el as HTMLInputElement).disabled;
      const hidden = el.offsetParent === null;
      return !disabled && !hidden;
    })?.nativeElement;

    if (target) {
      setTimeout(() => target.focus(), 0);
    }
  }

  private resetControlsState(): void {
    if (!this.form) return;
    Object.values(this.form.controls).forEach(c => {
      c.markAsPristine();
      c.markAsUntouched();
      c.updateValueAndValidity({ onlySelf: true, emitEvent: false });
    });
  }

  private buildForm(): void {
    const config = this.config();
    if (!config) return;
    this.ready = false;
    const base = this.entity() ?? config.createFactory?.() ?? {};
    const group: Record<string, FormControl> = {};

    config.fields.forEach(field => {
      group[field.name] = new FormControl(
        base[field.name] ?? null,
        field.required ? [Validators.required] : []
      );
    });

    this.form = new FormGroup(group);
    this.applyStatePerAction();
    this.ready = true;
  }

  private patchEntity(): void {
    const config = this.config();
    if (!config || !this.form) return;
    const src = this.entity() ?? config.createFactory?.() ?? {};
    config.fields.forEach(f => {
      if (this.form.get(f.name)) {
        this.form.get(f.name)!.setValue(src[f.name] ?? null, { emitEvent: false });
      }
    });
  }

  private applyStatePerAction(): void {
    const config = this.config();
    if (!config || !this.form) return;
    const action = this.parameter() as ManageAction;

    config.fields.forEach(f => {
      const ctrl = this.form.get(f.name);
      if (!ctrl) return;

      const hidden = !!f.hiddenOn && f.hiddenOn.includes(action);
      const shouldDisableBecauseOfAction =
        action === 'delete' ||
        (f.disabledOn?.includes(action));

      // Якщо поле сховане — теж disable, щоб валідатор не блокував submit
      const needDisable = hidden || shouldDisableBecauseOfAction;

      if (needDisable && !ctrl.disabled) {
        ctrl.disable({ emitEvent: false });
      } else if (!needDisable && ctrl.disabled) {
        ctrl.enable({ emitEvent: false });
      }
    });
  }

  hide(): void {
    this.visible.set(false);

    if (this.form) {
      this.form.reset();
      this.resetControlsState();
    }
  }

  submit(): void {
    const parameter = this.parameter();
    if (parameter === 'undef') return;
    const action = parameter;

    let raw = {
      ...(this.entity() ?? this.config().createFactory?.()),
      ...this.form.getRawValue()
    };

    const config = this.config();
    if (config.mapOut) {
      raw = config.mapOut(raw);
    }

    this.actionPerformed.emit({
      action,
      entity: raw,
      entityType: config.entityType
    });
    this.hide();
  }

  isHidden(field: ManagePanelField): boolean {
    const action = this.parameter() as ManageAction;
    return !!field.hiddenOn && field.hiddenOn.includes(action);
  }

  actionLabel(): string {
    switch (this.parameter()) {
      case 'create': return 'Створити';
      case 'update': return 'Оновити';
      case 'delete': return this.config()?.deleteAction?.verb ?? 'Видалити';
      default: return 'OK';
    }
  }

  header(): string {
    const config = this.config();
    return config?.title || (config?.entityType ?? '');
  }

  entityDisplayName(): string {
    const entity = this.entity();
    if (!entity) return '';
    return this.config()?.displayName?.(entity)
      || entity.name
      || entity.number?.toString()
      || '';
  }

  isDeleteMode(): boolean {
    return this.parameter() === 'delete';
  }

  visibleFields(): ManagePanelField[] {
    const config = this.config();
    if (!config) return [];
    return config.fields.filter(f => !this.isHidden(f));
  }
}
