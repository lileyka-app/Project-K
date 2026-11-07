import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { AuthService } from '../../../../../authModule/services/auth-service/auth.service';
import { MessageService } from '@openng/optimus-ui/api';
import { provideRouter } from '@angular/router';
import { KurinService } from '../../../../services/kurin-service/kurin.service';
import { MemberService } from '../../../../services/member-service/member.service';
import { MemberDto } from '../../../../models/member.dto';
import { KurinBranch } from '../../../../models/enums/kurin-branch.enum';
import { PlastLevel } from '../../../../models/enums/plast-level.enum';
import { RosterComponent } from './roster';
import { KurinTagService } from '../../../../services/kurin-tag-service/kurin-tag.service';
import { KurinTagsDto, NO_KURIN_TAGS } from '../../../../models/kurin-tag.dto';
import { computed, signal } from '@angular/core';

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

describe('RosterComponent', () => {
  let component: RosterComponent;
  let fixture: ComponentFixture<RosterComponent>;

  /** Теги куреня, які бачить реєстр; без тегів — як курінь, що їх не вмикав. */
  let kurinTags: KurinTagsDto = NO_KURIN_TAGS;
  let exported: string[] | null = null;

  function tagServiceStub(): Partial<KurinTagService> {
    const current = signal(kurinTags);
    const byKey = computed(() => new Map(current().tags.map(tag => [tag.kurinTagKey, tag])));
    return {
      current,
      enabled: computed(() => current().enabled),
      canManage: computed(() => current().enabled && current().canManage),
      byKey,
      revision: signal(0),
      resolve: (keys: readonly string[] | null | undefined) =>
        (keys ?? []).map(key => byKey().get(key)!).filter(Boolean).sort((a, b) => a.name.localeCompare(b.name, 'uk')),
      ensureCurrent: () => undefined,
      load: () => of(current())
    };
  }

  function load(people: MemberDto[], branch = KurinBranch.UPYu): void {
    TestBed.resetTestingModule();

    TestBed.configureTestingModule({
      imports: [RosterComponent],
      providers: [
        // Застосунок дає його на рівні app.config; компонент лише споживає.
        MessageService,
        provideRouter([]),
        { provide: MemberService, useValue: { getAll: () => of(people) } },
        {
          provide: KurinService,
          useValue: {
            getByKey: () => of({ kurinKey: 'kurin-1', number: 3, branch }),
            exportRegistry: (_: string, columns: string[]) => {
              exported = columns;
              return of({ body: null, headers: { get: () => null } });
            }
          }
        },
        { provide: KurinTagService, useValue: tagServiceStub() },
        { provide: AuthService, useValue: { getAuthState: () => of({ kurinKey: 'kurin-1' }) } }
      ]
    });

    localStorage.clear();
    fixture = TestBed.createComponent(RosterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  afterEach(() => {
    localStorage.clear();
    kurinTags = NO_KURIN_TAGS;
    exported = null;
  });

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

    expect(component.allColumns().map(column => column.id)).not.toContain('mentoredGroups');
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

  describe('tags', () => {
    const tag = (kurinTagKey: string, name: string) => ({ kurinTagKey, name, colorHex: '#2F855A', memberCount: null });

    beforeEach(() => {
      kurinTags = { enabled: true, canManage: true, maxPerMember: 10, palette: [], tags: [tag('t-new', 'Новики'), tag('t-camp', 'Табір')] };
    });

    it('should keep only people who carry one of the chosen tags', () => {
      load([
        member({ lastName: 'Новик', tagKeys: ['t-new'] }),
        member({ lastName: 'Табірний', tagKeys: ['t-camp'] }),
        member({ lastName: 'Без тегів' })
      ]);

      component.tagFilter.set(['t-new']);
      expect(component.youth().map(person => person.lastName)).toEqual(['Новик']);

      component.tagFilter.set(['t-new', 't-camp']);
      expect(component.youth().map(person => person.lastName)).toEqual(['Новик', 'Табірний']);
    });

    it('should give each row its tag names to sort the tags column by', () => {
      load([member({ lastName: 'Обидва', tagKeys: ['t-camp', 't-new'] })]);

      expect(component.youth()[0].tagSort).toBe('Новики, Табір');
    });

    it('should offer the tags column only while the kurin uses tags', () => {
      load([member({})]);
      expect(component.allColumns().some(column => column.id === 'tags')).toBe(true);

      kurinTags = NO_KURIN_TAGS;
      load([member({})]);
      expect(component.allColumns().some(column => column.id === 'tags')).toBe(false);
    });

    it('should send the tags column to the export like any other', () => {
      load([member({})]);
      component.selectedColumnIds.set(['groupName', 'tags']);

      component.exportToExcel();

      expect(exported).toEqual(['groupName', 'tags']);
    });

    // The default columns hold «Теги» for every kurin; one with tags off must not export it empty.
    it('should leave the tags column out of the export while the kurin has tags off', () => {
      kurinTags = NO_KURIN_TAGS;
      load([member({})]);
      component.selectedColumnIds.set(['groupName', 'tags']);

      component.exportToExcel();

      expect(exported).toEqual(['groupName']);
    });
  });
});
