import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { KurinCardFieldsComponent } from './kurin-card-fields';
import { KurinService } from '../../services/kurin-service/kurin.service';

describe('KurinCardFieldsComponent', () => {
  let fixture: ComponentFixture<KurinCardFieldsComponent>;
  let component: KurinCardFieldsComponent;
  let kurins: MockedObject<KurinService>;

  function create(fields: { key: string; label: string; hidden: boolean }[]): void {
    kurins.getHideableCardFields.mockReturnValue(of(fields));
    fixture = TestBed.createComponent(KurinCardFieldsComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('kurinKey', 'k1');
    fixture.detectChanges();
  }

  beforeEach(async () => {
    kurins = {
      getHideableCardFields: vi.fn(),
      setHiddenCardFields: vi.fn()
    } as MockedObject<KurinService>;
    kurins.setHiddenCardFields.mockImplementation((_, hidden) => of([{ key: 'note', label: 'Примітка', hidden: hidden.includes('note') }]));
    await TestBed.configureTestingModule({
      imports: [KurinCardFieldsComponent],
      providers: [{ provide: KurinService, useValue: kurins }]
    }).compileComponents();
  });

  it('saves the fields the kurin switches off', () => {
    create([
      { key: 'middleName', label: 'По батькові', hidden: true },
      { key: 'note', label: 'Примітка', hidden: false }
    ]);
    expect(fixture.nativeElement.textContent).toContain('Примітка');

    (component as unknown as { setShown(key: string, shown: boolean): void }).setShown('note', false);
    (component as unknown as { setShown(key: string, shown: boolean): void }).setShown('middleName', true);
    component.save().subscribe();

    expect(kurins.setHiddenCardFields).toHaveBeenCalledWith('k1', ['note']);
  });

  it('sends nothing when the край leaves nothing to hide', () => {
    create([]);

    component.save().subscribe();

    expect(kurins.setHiddenCardFields).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('прибрати тут нічого');
  });
});
