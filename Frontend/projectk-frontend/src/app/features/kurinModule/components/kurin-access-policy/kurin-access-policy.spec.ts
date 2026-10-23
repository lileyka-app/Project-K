import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { KurinAccessPolicyComponent } from './kurin-access-policy';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { AccessPolicyCellDto, KurinAccessPolicyDto } from '../../models/kurin-access-policy.dto';
import { CapabilityReach, PolicyAudience } from '../../models/access-policy-labels.model';

const AUDIENCES: PolicyAudience[] = ['Zvyazkovyi', 'Vykhovnyk', 'Instruktor', 'Kurinnyy', 'KurinProvid', 'Hurtkovyy', 'GroupProvid', 'Youth'];

function cells(defaults: CapabilityReach[], youthLocked: boolean, effective: Partial<Record<PolicyAudience, CapabilityReach>> = {}): AccessPolicyCellDto[] {
  return AUDIENCES.map((audience, index) => ({
    audience,
    default: defaults[index],
    effective: effective[audience] ?? defaults[index],
    locked: audience === 'Zvyazkovyi' || (audience === 'Youth' && youthLocked)
  }));
}

function policy(contactsYouth: CapabilityReach = 'Kurin'): KurinAccessPolicyDto {
  return {
    audiences: AUDIENCES,
    capabilities: [
      { key: 'SeeContacts', group: 'MemberData', youthConfigurable: true, cells: cells(['Kurin', 'Kurin', 'Kurin', 'Kurin', 'Kurin', 'Kurin', 'Kurin', 'Kurin'], false, { Youth: contactsYouth }) },
      { key: 'GiveWarning1', group: 'WarningsAwards', youthConfigurable: false, cells: cells(['Kurin', 'OwnGroup', 'OwnGroup', 'None', 'None', 'None', 'None', 'None'], true) },
      { key: 'ReadJournal', group: 'Journal', youthConfigurable: false, cells: cells(['Kurin', 'Kurin', 'Kurin', 'None', 'None', 'None', 'None', 'None'], true) }
    ]
  };
}

describe('KurinAccessPolicyComponent', () => {
  let fixture: ComponentFixture<KurinAccessPolicyComponent>;
  let component: KurinAccessPolicyComponent;
  let kurins: MockedObject<KurinService>;

  const el = () => fixture.nativeElement as HTMLElement;
  const cell = (capability: string, audience: string) => el().querySelector(`[data-testid="access-cell-${capability}-${audience}"]`);

  function create(loaded: KurinAccessPolicyDto = policy(), phone = false): void {
    vi.spyOn(window, 'matchMedia').mockReturnValue({
      matches: phone,
      media: '',
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      addListener: () => undefined,
      removeListener: () => undefined,
      dispatchEvent: () => false
    } as MediaQueryList);
    kurins.getAccessPolicy.mockReturnValue(of(loaded));
    fixture = TestBed.createComponent(KurinAccessPolicyComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('kurinKey', 'k1');
    fixture.detectChanges();
  }

  beforeEach(async () => {
    kurins = {
      getAccessPolicy: vi.fn(),
      setAccessPolicy: vi.fn()
    } as MockedObject<KurinService>;
    kurins.setAccessPolicy.mockImplementation(() => of(policy('OwnGroup')));

    await TestBed.configureTestingModule({
      imports: [KurinAccessPolicyComponent],
      providers: [{ provide: KurinService, useValue: kurins }]
    }).compileComponents();
  });

  it('lays the rights out by section and audience, with the Звʼязковий locked', () => {
    create();

    expect(el().querySelector('[data-testid="access-policy"]')).not.toBeNull();
    expect(el().textContent).toContain('Дані юнаків');
    expect(el().textContent).toContain('Перестороги й відзначення');
    expect(el().textContent).toContain('Пошта й телефон');
    expect(el().textContent).toContain('Провід гуртка');
    expect(cell('SeeContacts', 'Youth')).not.toBeNull();
    expect(component.groups()[0].rows[0].cells[0]).toEqual(expect.objectContaining({ audience: 'Zvyazkovyi', locked: true }));
    expect(el().textContent).toContain('Свої дані людина бачить завжди.');
  });

  it('marks a cell that differs from the default and says what the default is', () => {
    create(policy('OwnGroup'));

    const youth = component.groups()[0].rows[0].cells.find(c => c.audience === 'Youth');
    expect(youth).toEqual(expect.objectContaining({ value: 'OwnGroup', changed: true, defaultHint: 'За замовчуванням: Увесь курінь' }));
    expect(cell('SeeContacts', 'Youth')?.textContent).toContain('змінено');
  });

  it('sends the whole matrix once something changed', () => {
    create();
    component.set('SeeContacts', 'Youth', 'OwnGroup');
    component.set('SeeContacts', 'Zvyazkovyi', 'None');

    component.save().subscribe();

    expect(kurins.setAccessPolicy).toHaveBeenCalledTimes(1);
    const [kurinKey, request] = kurins.setAccessPolicy.mock.calls[0];
    expect(kurinKey).toBe('k1');
    expect(request.grants.SeeContacts).toEqual(expect.objectContaining({ Youth: 'OwnGroup', Zvyazkovyi: 'Kurin' }));
    expect(Object.keys(request.grants)).toEqual(['SeeContacts', 'GiveWarning1', 'ReadJournal']);
    expect(component.dirty()).toBe(false);
  });

  it('sends nothing when nothing changed', () => {
    create();

    component.save().subscribe();

    expect(kurins.setAccessPolicy).not.toHaveBeenCalled();
  });

  it('puts every cell back to its default on screen only', () => {
    create(policy('None'));
    expect(component.customised()).toBe(true);

    component.resetToDefaults();

    expect(component.customised()).toBe(false);
    expect(component.dirty()).toBe(true);
    expect(kurins.setAccessPolicy).not.toHaveBeenCalled();
    expect(el().querySelector('[data-testid="access-reset"]')).not.toBeNull();
  });

  it('gives a phone a card per section and leaves out the fixed youth cell of an action', () => {
    create(policy(), true);

    expect(el().querySelectorAll('.access-card').length).toBe(3);
    expect(el().querySelector('p-table')).toBeNull();
    expect(cell('SeeContacts', 'Youth')).not.toBeNull();
    expect(cell('GiveWarning1', 'Youth')).toBeNull();
    expect(cell('GiveWarning1', 'Vykhovnyk')?.querySelector('p-selectbutton')).not.toBeNull();
  });

  it('says so when the policy could not be read, and saves nothing', () => {
    kurins.getAccessPolicy.mockReturnValue(throwError(() => new Error('403')));
    fixture = TestBed.createComponent(KurinAccessPolicyComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('kurinKey', 'k1');
    fixture.detectChanges();

    expect(el().textContent).toContain('Не вдалося завантажити права й видимість');
    component.save().subscribe();
    expect(kurins.setAccessPolicy).not.toHaveBeenCalled();
  });
});
