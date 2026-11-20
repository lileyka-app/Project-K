import type { MockedObject } from 'vitest';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { of, throwError } from 'rxjs';
import { archiveDate, KurinArchiveComponent } from './archive';
import { AuthService } from '../../../../../authModule/services/auth-service/auth.service';
import { FormerMemberStandingDto, MembershipRequestDto, MembershipService } from '../../../../services/membership-service/membership.service';
import { RegistryModes } from '../../registry-modes';
import { KurinBranch } from '../../../../models/enums/kurin-branch.enum';
import { KurinArchiveService } from '../../../../services/kurin-archive-service/kurin-archive.service';
import { KurinFeaturesService } from '../../../../services/kurin-features-service/kurin-features.service';
import { KurinArchiveDto } from '../../../../models/kurin-archive.dto';
import { LeadershipRole } from '../../../../models/enums/leadership-role.enum';
import { MembershipKind } from '../../../../models/enums/membership-kind.enum';

describe('KurinArchiveComponent', () => {
  let fixture: ComponentFixture<KurinArchiveComponent>;
  let component: KurinArchiveComponent;
  let service: MockedObject<KurinArchiveService>;
  let memberships: MockedObject<MembershipService>;
  const hasGroups = signal(true);
  /** Whether the viewer takes former members back: the Звʼязковий does, a гурток's провід does not. */
  let canReturn = true;
  let query: Record<string, string> = {};
  let standing: FormerMemberStandingDto[] = [];
  let outgoing: MembershipRequestDto[] = [];

  function archive(overrides: Partial<KurinArchiveDto> = {}): KurinArchiveDto {
    return {
      offices: [
        {
          historyKey: 'h1', type: 'Kurin', role: LeadershipRole.Pysar, memberKey: 'm1', memberName: 'Ірина Мельник',
          startDate: '2021-09-01', endDate: '2023-08-31', groupKey: null, groupName: null, groupDissolved: false, canCorrect: true, isOwn: false
        },
        {
          historyKey: 'h2', type: 'KV', role: LeadershipRole.Zvyazkovyi, memberKey: 'me', memberName: 'Я',
          startDate: '2024-09-01', endDate: null, groupKey: null, groupName: null, groupDissolved: false, canCorrect: false, isOwn: true
        }
      ],
      memberships: [
        {
          membershipKey: 'ms1', memberKey: 'm2', memberName: 'Марко Бойко', kind: MembershipKind.Youth,
          joinedOn: '2017-09-12', leftOn: '2023-06-30',
          groups: [{ groupKey: 'g1', groupName: 'Беркути', from: null, to: '2025-01-01' }, { groupKey: 'g2', groupName: 'Соколи', from: '2025-01-01', to: '2025-06-30' }],
          canCorrect: true, isOwn: false
        }
      ],
      dissolvedGroups: [{ groupKey: 'g1', name: 'Беркути', dissolvedOn: '2025-01-01' }],
      canDelete: true,
      ...overrides
    };
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  beforeEach(async () => {
    service = {
      get: vi.fn(),
      correctOffice: vi.fn(),
      correctMembership: vi.fn(),
      enterDangerZone: vi.fn(),
      endDangerZone: vi.fn(),
      endDangerZoneOnUnload: vi.fn(),
      deleteOffice: vi.fn(),
      deleteMembership: vi.fn(),
      deleteGroup: vi.fn()
    } as unknown as MockedObject<KurinArchiveService>;
    service.get.mockReturnValue(of(archive()));
    service.endDangerZone.mockReturnValue(of(undefined));
    hasGroups.set(true);
    memberships = { formerStanding: vi.fn(), requests: vi.fn(), createRequest: vi.fn() } as unknown as MockedObject<MembershipService>;
    memberships.formerStanding.mockImplementation(() => of(standing));
    memberships.requests.mockImplementation(() => of({ incoming: [], outgoing }));

    await createComponent();
  });

  async function createComponent(): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [KurinArchiveComponent],
      providers: [
        provideRouter([]),
        MessageService,
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } },
        { provide: AuthService, useValue: { getAuthStateValue: () => ({ kurinKey: 'k1' }) } },
        { provide: MembershipService, useValue: memberships },
        { provide: RegistryModes, useValue: { allows: (mode: string) => mode !== 'transfers' || canReturn } },
        { provide: KurinArchiveService, useValue: service },
        { provide: KurinFeaturesService, useValue: { hasGroups, branch: signal(null) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(KurinArchiveComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  afterEach(() => {
    canReturn = true;
    query = {};
    standing = [];
    outgoing = [];
  });

  it('reads dates as text, without a time zone to shift them', () => {
    expect(archiveDate('2023-08-31')).toBe('31.08.2023');
    expect(archiveDate(null)).toBe('');
  });

  it('shows former holdings by default and current ones on «Усі»', () => {
    expect(component.offices().map(office => office.historyKey)).toEqual(['h1']);
    component.officeFilter.set('all');
    expect(component.offices().map(office => office.historyKey)).toEqual(['h1', 'h2']);
  });

  it('lists the гуртки a membership passed through', () => {
    expect(text()).toContain('Беркути → Соколи');
  });

  it('has no гуртки column or tile in a senior kurin', () => {
    hasGroups.set(false);
    fixture.detectChanges();
    expect(text()).not.toContain('Розформовані гуртки');
    expect(text()).not.toContain('Беркути → Соколи');
  });

  it('offers deletion only inside the danger zone, and never on one\'s own record', () => {
    const own = archive().offices[1];
    const former = archive().offices[0];
    expect(component.canDeleteOffice(former)).toBe(false);

    service.enterDangerZone.mockReturnValue(of({ ticket: 't', expiresAtUtc: new Date(Date.now() + 600_000).toISOString(), deletionsLeft: 10 }));
    component.openEnter();
    component.proof.set('secret');
    component.enter();

    expect(component.zone()?.ticket).toBe('t');
    expect(component.canDeleteOffice(former)).toBe(true);
    expect(component.canDeleteOffice({ ...own, endDate: '2025-01-01' })).toBe(false);
  });

  it('says what went wrong with the password, and opens nothing', () => {
    service.enterDangerZone.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'WrongCredentials' } })));
    component.openEnter();
    component.proof.set('wrong');
    component.enter();

    expect(component.zone()).toBeNull();
    expect(component.proofError()).toBe('Неправильний пароль');
  });

  it('ends the zone when the page goes away', () => {
    service.enterDangerZone.mockReturnValue(of({ ticket: 't', expiresAtUtc: new Date(Date.now() + 600_000).toISOString(), deletionsLeft: 10 }));
    component.openEnter();
    component.proof.set('secret');
    component.enter();

    const add = vi.spyOn(TestBed.inject(MessageService), 'add');

    fixture.destroy();

    expect(service.endDangerZone).toHaveBeenCalledWith('k1', 'Left');
    // Another mode of the registry is leaving too: the person is told the zone is off.
    expect(add).toHaveBeenCalledWith({ severity: 'info', summary: 'Небезпечний режим вимкнено' });
  });

  describe('former members', () => {
    const elsewhere = { kurinKey: 'k7', number: 7, namedAfter: null, branches: [{ branch: KurinBranch.UPYu, number: 7 }] };
    const person = (memberKey: string, standsIn: typeof elsewhere[] = []): FormerMemberStandingDto => ({
      former: { memberKey, firstName: 'Марко', lastName: 'Бойко', joinedAtUtc: '2017-09-12', leftAtUtc: '2023-06-30' },
      standsIn,
      offer: { seniorKurin: false, joinsAtOnce: standsIn.length === 0, personAnswers: false, transferAllowed: true, askKurins: standsIn }
    });
    const button = (label: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .filter(b => b.textContent?.includes(label));

    it('are taken back from their row, with where they stand now', async () => {
      standing = [person('m2')];
      await createComponent();

      expect(text()).toContain('Ніде');
      button('Повернути')[0].click();
      expect(component.returning()?.former.memberKey).toBe('m2');
    });

    it('are asked for from the kurin they stand in, and not twice', async () => {
      standing = [person('m2', [elsewhere])];
      await createComponent();
      expect(button('Попросити')).toHaveLength(1);

      outgoing = [{ memberKey: 'm2', status: 'Pending' } as MembershipRequestDto];
      await createComponent();
      expect(button('Попросити')).toHaveLength(0);
      expect(text()).toContain('Запит чекає');
    });

    it('are taken back from their latest membership only', async () => {
      standing = [person('m2')];
      const twice = archive().memberships[0];
      service.get.mockReturnValue(of(archive({
        memberships: [twice, { ...twice, membershipKey: 'ms0', joinedOn: '2012-09-01', leftOn: '2014-06-30' }]
      })));
      await createComponent();

      expect(button('Повернути')).toHaveLength(1);
      expect(component.returnFor(component.memberships()[1])).toBeNull();
    });

    it('are only read and corrected by the провід of a гурток', async () => {
      canReturn = false;
      standing = [person('m2')];
      memberships.formerStanding.mockClear();
      await createComponent();

      expect(memberships.formerStanding).not.toHaveBeenCalled();
      expect(button('Повернути')).toHaveLength(0);
      expect(text()).not.toContain('Зараз');
    });

    it('come into view on the address a card sends a released person to', async () => {
      query = { former: '1' };
      const scrolled: Element[] = [];
      const original = Element.prototype.scrollIntoView;
      Element.prototype.scrollIntoView = function (this: Element) { scrolled.push(this); };
      try {
        await createComponent();
        await new Promise(resolve => setTimeout(resolve));
      } finally {
        Element.prototype.scrollIntoView = original;
      }

      expect(scrolled.map(element => element.id)).toEqual(['former']);
    });
  });

  it('closes the zone once its last deletion is spent', () => {
    service.enterDangerZone.mockReturnValue(of({ ticket: 't', expiresAtUtc: new Date(Date.now() + 600_000).toISOString(), deletionsLeft: 1 }));
    service.deleteOffice.mockReturnValue(of({ deletionsLeft: 0 }));
    component.openEnter();
    component.proof.set('secret');
    component.enter();

    // The confirmation is the library's; accept it straight away.
    const confirmation = fixture.debugElement.injector.get(ConfirmationService);
    vi.spyOn(confirmation, 'confirm').mockImplementation(config => {
      config.accept?.();
      return confirmation;
    });
    component.confirmDeleteOffice(archive().offices[0]);

    expect(service.deleteOffice).toHaveBeenCalledWith('k1', 'h1', 't');
    expect(component.zone()).toBeNull();
  });

  it('says why a membership with dues stays', () => {
    service.enterDangerZone.mockReturnValue(of({ ticket: 't', expiresAtUtc: new Date(Date.now() + 600_000).toISOString(), deletionsLeft: 1 }));
    service.deleteMembership.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { error: 'ArchiveMembershipHasDues' } })));
    component.openEnter();
    component.proof.set('secret');
    component.enter();

    const confirmation = fixture.debugElement.injector.get(ConfirmationService);
    vi.spyOn(confirmation, 'confirm').mockImplementation(config => {
      config.accept?.();
      return confirmation;
    });
    const add = vi.spyOn(TestBed.inject(MessageService), 'add');
    component.confirmDeleteMembership(archive().memberships[0]);

    expect(add).toHaveBeenCalledWith({ severity: 'error', summary: 'За членством є внески' });
    expect(component.zone()).not.toBeNull();
  });
});
