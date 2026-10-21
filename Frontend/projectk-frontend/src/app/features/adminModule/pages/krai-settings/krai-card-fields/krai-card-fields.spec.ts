import { ComponentFixture, TestBed } from '@angular/core/testing';
import { KraiCardFieldsComponent, sameCardFields } from './krai-card-fields';

describe('KraiCardFieldsComponent', () => {
  let fixture: ComponentFixture<KraiCardFieldsComponent>;
  let component: KraiCardFieldsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [KraiCardFieldsComponent] }).compileComponents();
    fixture = TestBed.createComponent(KraiCardFieldsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('keeps only what differs from the card as it is', () => {
    component.setMode('phoneNumber', 'optional');
    expect(component.value()).toEqual({ fields: { phoneNumber: 'optional' }, extra: undefined });

    component.setMode('phoneNumber', null);
    expect(component.value()).toBeUndefined();
  });

  it('adds, edits and removes the край\'s own fields', () => {
    component.addField();
    component.addField();
    component.updateField(0, { key: 'visaType', label: 'Тип візи', required: true });
    component.updateField(1, { type: 'date', label: 'Віза до' });
    fixture.detectChanges();

    expect(component.extra().map(field => field.key)).toEqual(['visaType', 'field2']);
    expect(fixture.nativeElement.querySelectorAll('[data-testid="krai-extra"] li')).toHaveSize(2);

    component.removeField(0);
    expect(component.value()?.extra).toEqual([{ key: 'field2', label: 'Віза до', type: 'date', required: false }]);
  });

  it('compares sections whatever their key order', () => {
    expect(sameCardFields(
      { fields: { middleName: 'hidden', phoneNumber: 'optional' } },
      { fields: { phoneNumber: 'optional', middleName: 'hidden' }, extra: [] })).toBeTrue();
    expect(sameCardFields(undefined, { extra: [{ key: 'a', label: 'A', type: 'text', required: false }] })).toBeFalse();
  });
});
