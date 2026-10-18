import { ComponentFixture, TestBed } from '@angular/core/testing';
import { KraiConcessionsComponent } from './krai-concessions';

describe('KraiConcessionsComponent', () => {
  let fixture: ComponentFixture<KraiConcessionsComponent>;
  let component: KraiConcessionsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [KraiConcessionsComponent] }).compileComponents();
    fixture = TestBed.createComponent(KraiConcessionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('adds a пільга with a fresh key and a starting rule', () => {
    component.add();
    component.add();

    const items = component.value()!;
    expect(items.length).toBe(2);
    expect(items[0].key).toMatch(/^p-[a-z0-9]+$/);
    expect(items[0].key).not.toBe(items[1].key);
    expect(items[0]).toEqual(jasmine.objectContaining({ kind: 'percent', value: 10, appliesTo: ['stanytsia'] }));
  });

  it('drops the value of an exemption and keeps the parts in their order', () => {
    component.add();
    component.setKind(0, 'exempt');
    component.setPart(0, 'group', true);
    component.setPart(0, 'krai', true);

    const item = component.value()![0];
    expect('value' in item).toBeFalse();
    expect(item.appliesTo).toEqual(['stanytsia', 'krai', 'group']);
  });

  it('gives a sum off a starting amount and reads a typed number', () => {
    component.add();
    component.setKind(0, 'amount');
    component.setValue(0, '150');

    expect(component.value()![0].value).toBe(150);
    expect(component.unit(component.value()![0])).toBe('₴');
  });

  it('leaves nothing set when the last пільга is removed', () => {
    component.add();
    component.remove(0);

    expect(component.value()).toBeUndefined();
    expect(fixture.nativeElement.textContent).toContain('Власних пільг у краю немає');
  });
});
