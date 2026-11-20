import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { MembershipService } from '../../services/membership-service/membership.service';
import { KurinBranch } from '../../models/enums/kurin-branch.enum';
import { REGISTRY_MODES, RegistryMode, RegistryModes } from './registry-modes';
import { RegistryComponent } from './registry';

describe('RegistryComponent', () => {
  let allowed: RegistryMode[];
  let waiting: ReturnType<typeof vi.fn>;
  let kurin: object;

  beforeEach(() => {
    kurin = { kurinKey: 'k1', number: 12, branch: KurinBranch.UPYu };
  });

  function create() {
    waiting = vi.fn().mockReturnValue(of(2));
    TestBed.configureTestingModule({
      imports: [RegistryComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { getAuthState: () => new BehaviorSubject({ kurinKey: 'k1' }) } },
        { provide: KurinService, useValue: { getByKey: () => of(kurin) } },
        { provide: MembershipService, useValue: { waitingRequests: waiting } },
        {
          provide: RegistryModes,
          useValue: {
            allows: (mode: RegistryMode) => allowed.includes(mode),
            available: () => REGISTRY_MODES.filter(link => allowed.includes(link.mode))
          }
        }
      ]
    });
    const fixture = TestBed.createComponent(RegistryComponent);
    fixture.detectChanges();
    return fixture;
  }

  const tabs = (fixture: ReturnType<typeof create>) =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.page-tabs__tab')).map(tab => tab.textContent?.trim());

  it('names the kurin and offers each mode the person has, with the requests waiting on «Переходи»', () => {
    allowed = ['roster', 'transfers', 'archive', 'import'];
    const fixture = create();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('12 курінь · УПЮ');
    expect(tabs(fixture)).toEqual(['Склад', 'Переходи 2', 'Архів', 'Імпорт']);
    expect(waiting).toHaveBeenCalledWith('k1');
  });

  it('names a senior kurin by its name and every гілка', () => {
    allowed = ['roster'];
    kurin = {
      kurinKey: 'k1', number: 5, branch: KurinBranch.USP, namedAfter: 'Орден Хрестоносців',
      branches: [{ branch: KurinBranch.USP, number: 5 }, { branch: KurinBranch.UPS, number: 25 }]
    };
    const fixture = create();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Орден Хрестоносців · 5-й курінь УСП · 25-й курінь УПС');
  });

  it('shows a гурток\'s провід only their modes, and asks nothing about requests', () => {
    allowed = ['roster', 'archive'];
    const fixture = create();

    expect(tabs(fixture)).toEqual(['Склад', 'Архів']);
    expect(waiting).not.toHaveBeenCalled();
  });

  it('has no tabs for a single mode', () => {
    allowed = ['roster'];
    const fixture = create();

    expect(tabs(fixture)).toEqual([]);
  });
});
