import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ManagePanelComponent, ManagePanelConfig, ManagePanelField } from './manage-panel';
import { SimpleChange } from '@angular/core';
import { Validators } from '@angular/forms';

describe('ManagePanelComponent', () => {
  let component: ManagePanelComponent;
  let fixture: ComponentFixture<ManagePanelComponent>;

  const mockConfig: ManagePanelConfig = {
    entityType: 'TestEntity',
    title: 'Test Entity Management',
    fields: [
      {
        name: 'id',
        label: 'ID',
        type: 'number',
        required: false,
        disabledOn: ['update', 'delete']
      },
      {
        name: 'name',
        label: 'Name',
        type: 'text',
        placeholder: 'Enter name',
        required: true
      },
      {
        name: 'description',
        label: 'Description',
        type: 'textarea',
        required: false,
        hiddenOn: ['delete']
      },
      {
        name: 'value',
        label: 'Value',
        type: 'number',
        required: true
      }
    ],
    createFactory: () => ({ id: null, name: '', description: '', value: 0 }),
    mapOut: (raw) => ({ ...raw, mapped: true })
  };

  const mockEntity = {
    id: 1,
    name: 'Test Item',
    description: 'Test Description',
    value: 100
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ManagePanelComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ManagePanelComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Component initialization', () => {
    it('should initialize with default values', () => {
      expect(component.visible()).toBeFalse();
      expect(component.parameter()).toBe('undef');
      expect(component.entity()).toBeNull();
      expect(component.ready).toBeFalse();
    });

    it('should build form when config is provided', () => {
      fixture.componentRef.setInput('config', mockConfig);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });

      expect(component.form).toBeDefined();
      expect(component.ready).toBeTrue();
      expect(Object.keys(component.form.controls)).toHaveSize(4);
    });
  });

  describe('ngOnChanges', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('config', mockConfig);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });
    });

    it('should rebuild form when config changes', () => {
      const newConfig = { ...mockConfig, entityType: 'NewEntity' };
      
      fixture.componentRef.setInput('config', newConfig);
      component.ngOnChanges({
        config: new SimpleChange(mockConfig, newConfig, false)
      });

      expect(component.form).toBeDefined();
      expect(component.ready).toBeTrue();
    });

    it('should patch entity when entity changes', () => {
      fixture.componentRef.setInput('entity', mockEntity);
      component.ngOnChanges({
        entity: new SimpleChange(null, mockEntity, false)
      });

      expect(component.form.get('name')?.value).toBe('Test Item');
      expect(component.form.get('description')?.value).toBe('Test Description');
      expect(component.form.get('value')?.value).toBe(100);
    });

    it('should apply state when parameter changes', () => {
      fixture.componentRef.setInput('parameter', 'create');
      component.ngOnChanges({
        parameter: new SimpleChange('undef', 'create', false)
      });

      expect(component.form.get('id')?.disabled).toBeFalse();
    });

    it('should reset form when creating new entity', () => {
      fixture.componentRef.setInput('parameter', 'create');
      fixture.componentRef.setInput('visible', true);
      
      component.ngOnChanges({
        visible: new SimpleChange(false, true, false),
        parameter: new SimpleChange('undef', 'create', false)
      });

      expect(component.form.get('name')?.pristine).toBeTrue();
      expect(component.form.get('name')?.untouched).toBeTrue();
    });

    it('should patch entity when visible for update', () => {
      fixture.componentRef.setInput('parameter', 'update');
      fixture.componentRef.setInput('entity', mockEntity);
      fixture.componentRef.setInput('visible', true);

      component.ngOnChanges({
        visible: new SimpleChange(false, true, false)
      });

      expect(component.form.get('name')?.value).toBe('Test Item');
    });
  });

  describe('buildForm', () => {
    it('should create form controls for all fields', () => {
      fixture.componentRef.setInput('config', mockConfig);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });

      expect(component.form.get('id')).toBeDefined();
      expect(component.form.get('name')).toBeDefined();
      expect(component.form.get('description')).toBeDefined();
      expect(component.form.get('value')).toBeDefined();
    });

    it('should add required validators to required fields', () => {
      fixture.componentRef.setInput('config', mockConfig);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });

      const nameControl = component.form.get('name');
      const descriptionControl = component.form.get('description');

      expect(nameControl?.hasValidator(Validators.required)).toBeTrue();
      expect(descriptionControl?.hasValidator(Validators.required)).toBeFalse();
    });

    it('should use createFactory when no entity provided', () => {
      fixture.componentRef.setInput('config', mockConfig);
      fixture.componentRef.setInput('entity', null);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });

      expect(component.form.get('name')?.value).toBe('');
      expect(component.form.get('value')?.value).toBe(0);
    });

    it('should use entity values when provided', () => {
      fixture.componentRef.setInput('config', mockConfig);
      fixture.componentRef.setInput('entity', mockEntity);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });

      expect(component.form.get('name')?.value).toBe('Test Item');
      expect(component.form.get('value')?.value).toBe(100);
    });
  });

  describe('applyStatePerAction', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('config', mockConfig);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });
    });

    it('should disable all fields for delete action', () => {
      fixture.componentRef.setInput('parameter', 'delete');
      component.ngOnChanges({
        parameter: new SimpleChange('undef', 'delete', false)
      });

      expect(component.form.get('id')?.disabled).toBeTrue();
      expect(component.form.get('name')?.disabled).toBeTrue();
      expect(component.form.get('description')?.disabled).toBeTrue();
      expect(component.form.get('value')?.disabled).toBeTrue();
    });

    it('should disable fields based on disabledOn configuration for update', () => {
      fixture.componentRef.setInput('parameter', 'update');
      component.ngOnChanges({
        parameter: new SimpleChange('undef', 'update', false)
      });

      expect(component.form.get('id')?.disabled).toBeTrue();
      expect(component.form.get('name')?.disabled).toBeFalse();
    });

    it('should enable all non-restricted fields for create', () => {
      fixture.componentRef.setInput('parameter', 'create');
      component.ngOnChanges({
        parameter: new SimpleChange('undef', 'create', false)
      });

      expect(component.form.get('id')?.disabled).toBeFalse();
      expect(component.form.get('name')?.disabled).toBeFalse();
      expect(component.form.get('description')?.disabled).toBeFalse();
      expect(component.form.get('value')?.disabled).toBeFalse();
    });

    it('should disable hidden fields', () => {
      fixture.componentRef.setInput('parameter', 'delete');
      component.ngOnChanges({
        parameter: new SimpleChange('undef', 'delete', false)
      });

      expect(component.form.get('description')?.disabled).toBeTrue();
    });
  });

  describe('hide', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('config', mockConfig);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });
      fixture.componentRef.setInput('visible', true);
    });

    it('should set visible to false', () => {
      component.hide();
      expect(component.visible()).toBeFalse();
    });

    it('should emit visibleChange event', () => {
      let emitted: boolean | undefined;
      component.visible.subscribe(v => emitted = v);
      component.hide();
      expect(emitted).toBeFalse();
    });

    it('should reset form', () => {
      component.form.get('name')?.setValue('Test');
      component.hide();
      expect(component.form.get('name')?.value).toBeNull();
    });

    it('should reset control states', () => {
      const nameControl = component.form.get('name');
      nameControl?.setValue('Test');
      nameControl?.markAsDirty();
      nameControl?.markAsTouched();

      component.hide();

      expect(nameControl?.pristine).toBeTrue();
      expect(nameControl?.untouched).toBeTrue();
    });
  });

  describe('submit', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('config', mockConfig);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });
      fixture.componentRef.setInput('visible', true);
    });

    it('should not submit when parameter is undef', () => {
      spyOn(component.actionPerformed, 'emit');
      fixture.componentRef.setInput('parameter', 'undef');
      component.submit();
      expect(component.actionPerformed.emit).not.toHaveBeenCalled();
    });

    it('should emit actionPerformed for create action', () => {
      spyOn(component.actionPerformed, 'emit');
      fixture.componentRef.setInput('parameter', 'create');
      component.form.patchValue({ name: 'New Item', value: 50 });

      component.submit();

      expect(component.actionPerformed.emit).toHaveBeenCalledWith({
        action: 'create',
        entity: jasmine.objectContaining({
          name: 'New Item',
          value: 50,
          mapped: true
        }),
        entityType: 'TestEntity'
      });
    });

    it('should emit actionPerformed for update action', () => {
      spyOn(component.actionPerformed, 'emit');
      fixture.componentRef.setInput('parameter', 'update');
      fixture.componentRef.setInput('entity', mockEntity);
      component.ngOnChanges({
        entity: new SimpleChange(null, mockEntity, false)
      });
      component.form.patchValue({ name: 'Updated Item' });

      component.submit();

      expect(component.actionPerformed.emit).toHaveBeenCalledWith({
        action: 'update',
        entity: jasmine.objectContaining({
          id: 1,
          name: 'Updated Item',
          mapped: true
        }),
        entityType: 'TestEntity'
      });
    });

    it('should emit actionPerformed for delete action', () => {
      spyOn(component.actionPerformed, 'emit');
      fixture.componentRef.setInput('parameter', 'delete');
      fixture.componentRef.setInput('entity', mockEntity);
      component.ngOnChanges({
        entity: new SimpleChange(null, mockEntity, false)
      });

      component.submit();

      expect(component.actionPerformed.emit).toHaveBeenCalledWith({
        action: 'delete',
        entity: jasmine.objectContaining({
          id: 1,
          mapped: true
        }),
        entityType: 'TestEntity'
      });
    });

    it('should apply mapOut function when provided', () => {
      spyOn(component.actionPerformed, 'emit');
      fixture.componentRef.setInput('parameter', 'create');
      component.form.patchValue({ name: 'Test', value: 10 });

      component.submit();

      const emittedEntity = (component.actionPerformed.emit as jasmine.Spy).calls.mostRecent().args[0].entity;
      expect(emittedEntity.mapped).toBeTrue();
    });

    it('should hide panel after submit', () => {
      spyOn(component, 'hide');
      fixture.componentRef.setInput('parameter', 'create');
      component.submit();
      expect(component.hide).toHaveBeenCalled();
    });

    it('should merge entity with form values', () => {
      spyOn(component.actionPerformed, 'emit');
      fixture.componentRef.setInput('parameter', 'update');
      fixture.componentRef.setInput('entity', { id: 1, name: 'Old', value: 100, extra: 'data' });
      component.ngOnChanges({
        entity: new SimpleChange(null, component.entity(), false)
      });
      component.form.patchValue({ name: 'New' });

      component.submit();

      const emittedEntity = (component.actionPerformed.emit as jasmine.Spy).calls.mostRecent().args[0].entity;
      expect(emittedEntity.id).toBe(1);
      expect(emittedEntity.name).toBe('New');
      expect(emittedEntity.extra).toBe('data');
    });
  });

  describe('isHidden', () => {
    const testField: ManagePanelField = {
      name: 'test',
      label: 'Test',
      type: 'text',
      hiddenOn: ['delete']
    };

    it('should return true when field is hidden for current action', () => {
      fixture.componentRef.setInput('parameter', 'delete');
      expect(component.isHidden(testField)).toBeTrue();
    });

    it('should return false when field is not hidden for current action', () => {
      fixture.componentRef.setInput('parameter', 'create');
      expect(component.isHidden(testField)).toBeFalse();
    });

    it('should return false when field has no hiddenOn config', () => {
      const fieldWithoutHidden: ManagePanelField = {
        name: 'test',
        label: 'Test',
        type: 'text'
      };
      fixture.componentRef.setInput('parameter', 'delete');
      expect(component.isHidden(fieldWithoutHidden)).toBeFalse();
    });
  });

  describe('actionLabel', () => {
    it('should return "Створити" for create action', () => {
      fixture.componentRef.setInput('parameter', 'create');
      expect(component.actionLabel()).toBe('Створити');
    });

    it('should return "Оновити" for update action', () => {
      fixture.componentRef.setInput('parameter', 'update');
      expect(component.actionLabel()).toBe('Оновити');
    });

    it('should return "Видалити" for delete action', () => {
      fixture.componentRef.setInput('parameter', 'delete');
      expect(component.actionLabel()).toBe('Видалити');
    });

    it('should return "OK" for undefined action', () => {
      fixture.componentRef.setInput('parameter', 'undef');
      expect(component.actionLabel()).toBe('OK');
    });
  });

  describe('header', () => {
    it('should return config title when provided', () => {
      fixture.componentRef.setInput('config', mockConfig);
      expect(component.header()).toBe('Test Entity Management');
    });

    it('should return entityType when title is not provided', () => {
      const configWithoutTitle = { ...mockConfig, title: undefined };
      fixture.componentRef.setInput('config', configWithoutTitle);
      expect(component.header()).toBe('TestEntity');
    });

    it('should return empty string when config is not set', () => {
      fixture.componentRef.setInput('config', undefined as unknown as ManagePanelConfig);
      expect(component.header()).toBe('');
    });
  });

  describe('isDeleteMode', () => {
    it('should return true when parameter is delete', () => {
      fixture.componentRef.setInput('parameter', 'delete');
      expect(component.isDeleteMode()).toBeTrue();
    });

    it('should return false when parameter is not delete', () => {
      fixture.componentRef.setInput('parameter', 'create');
      expect(component.isDeleteMode()).toBeFalse();

      fixture.componentRef.setInput('parameter', 'update');
      expect(component.isDeleteMode()).toBeFalse();

      fixture.componentRef.setInput('parameter', 'undef');
      expect(component.isDeleteMode()).toBeFalse();
    });
  });

  describe('visibleFields', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('config', mockConfig);
    });

    it('should return all fields when no fields are hidden', () => {
      fixture.componentRef.setInput('parameter', 'create');
      const visible = component.visibleFields();
      expect(visible).toHaveSize(4);
    });

    it('should filter out hidden fields', () => {
      fixture.componentRef.setInput('parameter', 'delete');
      const visible = component.visibleFields();
      expect(visible).toHaveSize(3);
      expect(visible.find(f => f.name === 'description')).toBeUndefined();
    });

    it('should return empty array when config is not set', () => {
      fixture.componentRef.setInput('config', undefined as unknown as ManagePanelConfig);
      expect(component.visibleFields()).toHaveSize(0);
    });
  });

  describe('onDialogShow', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('config', mockConfig);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });
    });

    it('should not focus input in delete mode', () => {
      fixture.componentRef.setInput('parameter', 'delete');
      spyOn(component as unknown as { focusFirstInput: () => void }, 'focusFirstInput');
      
      component.onDialogShow();
      
      expect(component['focusFirstInput']).not.toHaveBeenCalled();
    });

    it('should focus first input in non-delete modes', () => {
      fixture.componentRef.setInput('parameter', 'create');
      spyOn(component as unknown as { focusFirstInput: () => void }, 'focusFirstInput');
      
      component.onDialogShow();
      
      expect(component['focusFirstInput']).toHaveBeenCalled();
    });
  });

  describe('Integration scenarios', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('config', mockConfig);
      component.ngOnChanges({
        config: new SimpleChange(null, mockConfig, true)
      });
    });

    it('should handle complete create flow', () => {
      spyOn(component.actionPerformed, 'emit');
      
      fixture.componentRef.setInput('parameter', 'create');
      fixture.componentRef.setInput('visible', true);
      component.ngOnChanges({
        parameter: new SimpleChange('undef', 'create', false),
        visible: new SimpleChange(false, true, false)
      });

      component.form.patchValue({
        name: 'New Entity',
        description: 'Description',
        value: 200
      });

      component.submit();

      expect(component.actionPerformed.emit).toHaveBeenCalledWith({
        action: 'create',
        entity: jasmine.objectContaining({
          name: 'New Entity',
          description: 'Description',
          value: 200
        }),
        entityType: 'TestEntity'
      });
      expect(component.visible()).toBeFalse();
    });

    it('should handle complete update flow', () => {
      spyOn(component.actionPerformed, 'emit');
      
      fixture.componentRef.setInput('parameter', 'update');
      fixture.componentRef.setInput('entity', mockEntity);
      fixture.componentRef.setInput('visible', true);
      
      component.ngOnChanges({
        entity: new SimpleChange(null, mockEntity, false),
        parameter: new SimpleChange('undef', 'update', false),
        visible: new SimpleChange(false, true, false)
      });

      component.form.patchValue({ name: 'Updated Name' });
      component.submit();

      expect(component.actionPerformed.emit).toHaveBeenCalledWith({
        action: 'update',
        entity: jasmine.objectContaining({
          id: 1,
          name: 'Updated Name'
        }),
        entityType: 'TestEntity'
      });
    });

    it('should handle complete delete flow', () => {
      spyOn(component.actionPerformed, 'emit');
      
      fixture.componentRef.setInput('parameter', 'delete');
      fixture.componentRef.setInput('entity', mockEntity);
      fixture.componentRef.setInput('visible', true);
      
      component.ngOnChanges({
        entity: new SimpleChange(null, mockEntity, false),
        parameter: new SimpleChange('undef', 'delete', false),
        visible: new SimpleChange(false, true, false)
      });

      component.submit();

      expect(component.actionPerformed.emit).toHaveBeenCalledWith({
        action: 'delete',
        entity: jasmine.objectContaining({ id: 1 }),
        entityType: 'TestEntity'
      });
    });

    it('should validate required fields on submit', () => {
      fixture.componentRef.setInput('parameter', 'create');
      fixture.componentRef.setInput('visible', true);
      
      component.ngOnChanges({
        parameter: new SimpleChange('undef', 'create', false),
        visible: new SimpleChange(false, true, false)
      });

      // Очищаємо required поля
      component.form.patchValue({
        name: '',
        value: null
      });

      // Don't fill required fields
      expect(component.form.valid).toBeFalse();
      expect(component.form.get('name')?.errors?.['required']).toBeTrue();
      expect(component.form.get('value')?.errors?.['required']).toBeTrue();
    });
  });
});