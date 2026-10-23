import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { AuthService } from '../../../authModule/services/auth-service/auth.service';
import { Confirmation, ConfirmationService, MessageService } from '@openng/optimus-ui/api';
import { provideRouter } from '@angular/router';
import { FormerMemberDto, MembershipService } from '../../services/membership-service/membership.service';
import { KurinService } from '../../services/kurin-service/kurin.service';
import { MemberService } from '../../services/member-service/member.service';
import { MemberDto } from '../../models/member.dto';
import { KurinBranch } from '../../models/enums/kurin-branch.enum';
import { PlastLevel } from '../../models/enums/plast-level.enum';
import { RegistryComponent } from './registry';

function member(partial: Partial<MemberDto>): MemberDto {
  return {
    memberKey: partial.lastName ?? 'k',
    groupKey: 'g',
    kurinKey: 'kurin-1',
    firstName: 'Тест',
    middleName: '',
    lastName: 'Особа',
    email: 'a@b.c',
    phoneNumber: '0500000000',
    dateOfBirth: null,
    plastLevelHistories: [],
    leadershipHistories: [],
    profilePhotoUrl: null,
    ...partial
  };
}

function formerPerson(partial: Partial<FormerMemberDto> = {}): FormerMemberDto {
  return {
    memberKey: partial.memberKey ?? 'gone-1',
    firstName: 'Тест',
    lastName: 'Вибула',
    groupName: 'Alpha',
    joinedAtUtc: '2024-01-01T00:00:00Z',
    leftAtUtc: '2026-09-01T00:00:00Z',
    ...partial
  };
}

describe('RegistryComponent', () => {
  let component: RegistryComponent;
  let fixture: ComponentFixture<RegistryComponent>;
  let membership: MockedObject<MembershipService>;

  /**
   * Натиснути «Так» у діалозі. `ConfirmationService` компонент дає сам, тож брати його треба з
   * інжектора компонента, а не з кореневого; сам діалог тут не рендериться, тож викликаємо те, що
   * викликав би він.
   */
  function acceptConfirmation(): void {
    const service = fixture.debugElement.injector.get(ConfirmationService);
    pendingConfirmations = [];
    vi.spyOn(service, 'confirm').mockImplementation(confirmation => {
      pendingConfirmations.push(confirmation);
      return service;
    });
  }

  let pendingConfirmations: Confirmation[] = [];

  /**
   * @param setup Підмінити відповідь сервісу до того, як компонент її попросить. Ставити стаб перед
   *   `load` марно: він перестворює шпигуна, і тест на помилку проходив би, бо порожній список
   *   виглядає так само, як невдале читання.
   */
  function load(
    people: MemberDto[],
    branch = KurinBranch.UPYu,
    former: FormerMemberDto[] = [],
    setup?: (service: MockedObject<MembershipService>) => void
  ): void {
    TestBed.resetTestingModule();
    membership = {
      former: vi.fn(),
      takeBack: vi.fn()
    } as MockedObject<MembershipService>;
    membership.former.mockReturnValue(of(former));
    membership.takeBack.mockReturnValue(of('membership-key'));

    TestBed.configureTestingModule({
      imports: [RegistryComponent],
      providers: [
        // Застосунок дає його на рівні app.config; компонент лише споживає.
        MessageService,
        provideRouter([]),
        { provide: MembershipService, useValue: membership },
        { provide: MemberService, useValue: { getAll: () => of(people) } },
        {
          provide: KurinService,
          useValue: { getByKey: () => of({ kurinKey: 'kurin-1', number: 3, branch }) }
        },
        { provide: AuthService, useValue: { getAuthState: () => of({ kurinKey: 'kurin-1' }) } }
      ]
    });

    setup?.(membership);

    localStorage.clear();
    fixture = TestBed.createComponent(RegistryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  afterEach(() => localStorage.clear());

  /**
   * Розділяє діловодство в кадрі виховників, і бекенд уже сказав, у кого він є. Гуртковий теж має діловодство і
   * теж юнак — якби ділили за «має діловодство», таблиця юнаків втратила б саме тих, хто веде гуртки.
   */
  it('should keep юнаки and впорядники in separate tables', () => {
    load([
      member({ lastName: 'Юнак' }),
      member({ lastName: 'Гуркова', userRole: 'Group.Hurtkoviy' }),
      member({ lastName: 'Виховна', isStaff: true, mentoredGroupNames: ['Alpha'] })
    ]);

    expect(component.youth().map(person => person.lastName)).toEqual(['Гуркова', 'Юнак']);
    expect(component.staff().map(person => person.lastName)).toEqual(['Виховна']);
  });

  it('should lead the staff table with the гуртки they mentor and drop their own', () => {
    load([member({ lastName: 'Виховна', isStaff: true, mentoredGroupNames: ['Alpha', 'Beta'] })]);

    const ids = component.staffColumns().map(column => column.id);

    expect(ids[0]).toBe('mentoredGroups');
    expect(ids).not.toContain('groupName');

    const assignment = component.staffColumns()[0];
    expect(component.cell(component.staff()[0], assignment)).toBe('Alpha, Beta');
  });

  it('should not offer the закріплення column in the chooser', () => {
    load([]);

    expect(component.allColumns.map(column => column.id)).not.toContain('mentoredGroups');
  });

  it('should count only юнаки, one row per ступінь', () => {
    load([
      member({ lastName: 'Один', latestPlastLevel: PlastLevel.Uchasnyk }),
      member({ lastName: 'Два', latestPlastLevel: PlastLevel.Uchasnyk }),
      member({ lastName: 'Три', latestPlastLevel: PlastLevel.Skob }),
      member({ lastName: 'Виховна', latestPlastLevel: PlastLevel.Uchasnyk, isStaff: true })
    ]);

    const counts = new Map(component.tally().map(row => [row.label, row.count]));

    expect(counts.get('пл. уч.')).toBe(2);
    expect(counts.get('пл. скоб / вірл.')).toBe(1);
    expect(counts.get('Разом')).toBe(3);
  });

  /** Незаписаний ступінь — саме те, заради чого цю табличку й читають. */
  it('should show people with no ступінь rather than losing them', () => {
    load([member({ lastName: 'Без' }), member({ lastName: 'Учасник', latestPlastLevel: PlastLevel.Uchasnyk })]);

    const counts = new Map(component.tally().map(row => [row.label, row.count]));

    expect(counts.get('Без ступеня / інший')).toBe(1);
    expect(counts.get('Разом')).toBe(2);
  });

  it('should count a УПС kurin on its own ladder', () => {
    load([member({ lastName: 'Сеньйор', latestPlastLevel: PlastLevel.SeniorPratsi })], KurinBranch.UPS);

    const counts = new Map(component.tally().map(row => [row.label, row.count]));

    expect(counts.get('пл. сен. праці')).toBe(1);
    expect(counts.has('Без ступеня / інший')).toBe(false);
  });

  /**
   * Головне в цій задачі. Виведеного не видно в жодному читанні складу — усі вони фільтрують
   * закриті членства, — тож без цього списку помилковий клік прибирав людину назовсім.
   */
  it('should show who the kurin let go, apart from the склад', () => {
    load([member({ lastName: 'Юнак' })], KurinBranch.UPYu, [formerPerson({ lastName: 'Вибула' })]);

    expect(component.youth().map(person => person.lastName)).toEqual(['Юнак']);
    expect(component.former().map(person => person.lastName)).toEqual(['Вибула']);
    expect(component.formerName(component.former()[0])).toBe('Вибула Тест');
  });

  /** Довідка, а не робота: список згорнутий, поки його не попросили. */
  it('should keep the former list folded away until asked', () => {
    load([], KurinBranch.UPYu, [formerPerson()]);

    expect(component.formerOpen()).toBe(false);
    component.toggleFormer();
    expect(component.formerOpen()).toBe(true);
  });

  it('should take someone back by their key, without the code they hold', () => {
    const person = formerPerson({ memberKey: 'gone-42' });
    load([], KurinBranch.UPYu, [person]);

    acceptConfirmation();
    component.confirmTakeBack(person);
    pendingConfirmations[0].accept?.();

    expect(membership.takeBack).toHaveBeenCalledTimes(1);

    expect(membership.takeBack).toHaveBeenCalledWith('kurin-1', 'gone-42');
    expect(component.former()).toEqual([]);
  });

  it('should leave them on the list when taking them back fails', () => {
    const person = formerPerson({ memberKey: 'gone-42' });
    load([], KurinBranch.UPYu, [person], service =>
      service.takeBack.mockReturnValue(throwError(() => new Error('nope'))));

    acceptConfirmation();
    component.confirmTakeBack(person);
    pendingConfirmations[0].accept?.();

    expect(component.former()).toHaveLength(1);
    expect(component.returning()).toBeNull();
  });

  /** Курінь, у якому ще нікого не виводили, не має бачити помилку через порожній список. */
  it('should stay quiet when the former list cannot be read', () => {
    load([member({ lastName: 'Юнак' })], KurinBranch.UPYu, [], service =>
      service.former.mockReturnValue(throwError(() => new Error('nope'))));

    expect(component.former()).toEqual([]);
    expect(component.youth()).toHaveLength(1);
  });

  it('should search across both tables at once', () => {
    load([
      member({ lastName: 'Юнак', groupName: 'Alpha' }),
      member({ lastName: 'Виховна', isStaff: true, mentoredGroupNames: ['Alpha'] }),
      member({ lastName: 'Стороння', groupName: 'Beta' })
    ]);

    component.search.set('alpha');

    expect(component.youth().map(person => person.lastName)).toEqual(['Юнак']);
    expect(component.staff().map(person => person.lastName)).toEqual(['Виховна']);
  });
});
