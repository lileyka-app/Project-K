import type { MockedObject } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { MessageService } from '@openng/optimus-ui/api';
import { KraiSettingsComponent } from './krai-settings';
import { KraiProfileService } from '../../services/krai-profile-service/krai-profile.service';
import { KurinService } from '../../../kurinModule/services/kurin-service/kurin.service';
import { BadgesCatalogService } from '../../../kurinModule/services/probes-and-badges/badges-catalog.service';
import { BadgeCatalogItemDto } from '../../../kurinModule/models/probes-and-badges/badge-catalog-item.dto';
import { KraiProfileEditorDto } from '../../models/krai-profile.dto';
import { DuesUpstream } from '../../../duesModule/models/dues.enums';

function badge(id: string, kraiCode: string, country: string): BadgeCatalogItemDto {
  return {
    id, title: id, imagePath: '', country, specialization: '', status: '', level: null,
    lastUpdated: '', seekerRequirements: '', instructorRequirements: '', fixNotes: [], kraiCode
  };
}

describe('KraiSettingsComponent', () => {
  let fixture: ComponentFixture<KraiSettingsComponent>;
  let component: KraiSettingsComponent;
  let profiles: MockedObject<KraiProfileService>;
  let messages: MessageService;

  const czProfile: KraiProfileEditorDto = {
    kraiCode: 'cz',
    kraiName: 'Чехія',
    revision: 2,
    document: { catalog: { badgeSources: ['ua'], hiddenBadges: ['ua-2'] }, locale: { currency: 'CZK' } }
  };

  beforeEach(async () => {
    profiles = {
      create: vi.fn(),
      rename: vi.fn(),
      get: vi.fn(),
      save: vi.fn(),
      revisions: vi.fn(),
      restore: vi.fn(),
      concessionReport: vi.fn()
    } as MockedObject<KraiProfileService>;
    profiles.concessionReport.mockReturnValue(of({ kraiCode: 'cz', kraiName: 'Чехія', currency: 'CZK', numberLocale: 'uk-UA', quarter: { year: 2026, number: 4 }, kurinCount: 1, concessions: [] }));
    profiles.get.mockReturnValue(of(czProfile));
    profiles.revisions.mockReturnValue(of([
      { revision: 2, createdAtUtc: '2026-10-14T10:00:00Z', createdByName: 'Адмін', note: 'Українські вмілості' },
      { revision: 1, createdAtUtc: '2026-10-13T10:00:00Z', createdByName: null, note: 'Початковий профіль' }
    ]));
    const kurins = {
      getKrais: vi.fn()
    };
    kurins.getKrais.mockReturnValue(of([{ code: 'cz', name: 'Чехія', kurinCount: 1 }]));
    const catalog = {
      getAll: vi.fn()
    };
    catalog.getAll.mockReturnValue(of([badge('ua-1', 'ua', 'Україна'), badge('ua-2', 'ua', 'Україна'), badge('pl-1', 'pl', 'Польща')]));

    await TestBed.configureTestingModule({
      imports: [KraiSettingsComponent],
      providers: [
        provideRouter([]),
        MessageService,
        { provide: KraiProfileService, useValue: profiles },
        { provide: KurinService, useValue: kurins },
        { provide: BadgesCatalogService, useValue: catalog }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(KraiSettingsComponent);
    component = fixture.componentInstance;
    messages = TestBed.inject(MessageService);
    fixture.detectChanges();
  });

  it('shows the badges of the chosen sources with the hidden one switched off', () => {
    expect(component.sourceBadges().map(b => b.id)).toEqual(['ua-1', 'ua-2']);
    expect(component.offeredCount()).toBe(1);
    expect(component.isOffered('ua-2')).toBe(false);
    expect(component.sourceOptions().map(o => o.code)).toEqual(['pl', 'ua']);
    expect(fixture.nativeElement.textContent).toContain('Ревізія 2');
  });

  it('saves a new revision on top of the one it loaded, keeping the other sections', () => {
    profiles.save.mockReturnValue(of({ ...czProfile, revision: 3, document: { catalog: { badgeSources: ['ua'] }, locale: { currency: 'CZK' } } }));

    component.setOffered('ua-2', true);
    expect(component.dirty()).toBe(true);
    component.note.set(' усе українське ');
    component.save();

    expect(profiles.save).toHaveBeenCalledWith('cz', 2, {
      catalog: { badgeSources: ['ua'], hiddenBadges: undefined },
      locale: { currency: 'CZK', timeZone: undefined, numberLocale: undefined },
      dues: undefined,
      cardFields: undefined
    }, 'усе українське');
    expect(component.editor()!.revision).toBe(3);
    expect(component.dirty()).toBe(false);
  });

  it('shows the server explanation when someone saved first', () => {
    vi.spyOn(messages, 'add').mockReturnValue(undefined);
    profiles.save.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { error: 'ProfileChanged', message: 'Профіль змінили, поки ти його редагував. Онови сторінку.' }
    })));

    component.setOffered('ua-1', false);
    component.save();

    expect(messages.add).toHaveBeenCalledWith(expect.objectContaining({ detail: 'Профіль змінили, поки ти його редагував. Онови сторінку.' }));
  });

  it('saves the currency, number format and time zone, leaving what it does not edit', () => {
    profiles.save.mockReturnValue(of(czProfile));

    expect(component.currency()).toBe('CZK');
    expect(component.timeZone()).toBeNull();
    expect(component.numberLocale()).toBeNull();
    component.timeZone.set('Europe/Prague');
    component.numberLocale.set('cs-CZ');
    component.upstream.set(DuesUpstream.Krai);
    expect(component.dirty()).toBe(true);
    expect(component.numberLocaleOptions().find(o => o.code === 'cs-CZ')?.label).toContain('1');
    component.save();

    expect(profiles.save).toHaveBeenCalledWith('cz', 2, {
      catalog: { badgeSources: ['ua'], hiddenBadges: ['ua-2'] },
      locale: { currency: 'CZK', timeZone: 'Europe/Prague', numberLocale: 'cs-CZ' },
      dues: { upstream: DuesUpstream.Krai },
      cardFields: undefined
    }, null);
  });

  it('saves the край\'s пільги inside the dues section', () => {
    profiles.save.mockReturnValue(of(czProfile));
    const concessions = [{ key: 'p-sib', name: 'Брат чи сестра', kind: 'percent' as const, value: 20, appliesTo: ['stanytsia' as const] }];

    component.concessions.set(concessions);
    expect(component.dirty()).toBe(true);
    component.save();

    expect(profiles.save).toHaveBeenCalledWith('cz', 2, expect.objectContaining({ dues: expect.objectContaining({ concessions }) }), null);
    expect(fixture.nativeElement.textContent).toContain('Пільги краю');
  });

  it('saves the card fields with the rest of the profile', () => {
    profiles.save.mockReturnValue(of(czProfile));
    const cardFields = {
      fields: { middleName: 'hidden' as const },
      extra: [{ key: 'visaType', label: 'Тип візи', type: 'text' as const, required: true }]
    };

    component.cardFields.set(cardFields);
    expect(component.dirty()).toBe(true);
    component.save();

    expect(profiles.save).toHaveBeenCalledWith('cz', 2, expect.objectContaining({ cardFields }), null);
    expect(fixture.nativeElement.textContent).toContain('Картка члена');
  });

  it('restores an earlier revision', () => {
    profiles.restore.mockReturnValue(of({ ...czProfile, revision: 3, document: {} }));

    component.restore(component.revisions()[1]);

    expect(profiles.restore).toHaveBeenCalledWith('cz', 1);
    expect(component.sources()).toEqual([]);
    expect(component.effectiveSources()).toEqual(['cz']);
  });

  it('adds a край and opens its profile', () => {
    profiles.create.mockReturnValue(of({ code: 'sk', name: 'Словаччина', kurinCount: 0 }));
    profiles.get.mockReturnValue(of({ kraiCode: 'sk', kraiName: 'Словаччина', revision: 1, document: { catalog: { badgeSources: ['ua'] } } }));

    component.startAdding();
    component.newCode.set(' SK ');
    component.newName.set(' Словаччина ');
    expect(component.newCodeValid()).toBe(true);
    component.addKrai();

    expect(profiles.create).toHaveBeenCalledWith('sk', 'Словаччина');
    expect(component.adding()).toBe(false);
    expect(component.krais().map(k => k.code)).toEqual(['sk', 'cz']);
    expect(component.kraiCode()).toBe('sk');
    expect(component.editor()!.kraiName).toBe('Словаччина');
  });

  it('does not send a code that cannot name a край', () => {
    component.startAdding();
    component.newCode.set('sk-1');
    component.newName.set('Словаччина');
    component.addKrai();

    expect(component.newCodeValid()).toBe(false);
    expect(profiles.create).not.toHaveBeenCalled();
  });

  it('renames the край, keeping its code', () => {
    profiles.rename.mockReturnValue(of({ code: 'cz', name: 'Пласт Чехія', kurinCount: 1 }));

    expect(component.renamed()).toBe(false);
    component.name.set('Пласт Чехія');
    component.renameKrai();

    expect(profiles.rename).toHaveBeenCalledWith('cz', 'Пласт Чехія');
    expect(component.krais()[0].name).toBe('Пласт Чехія');
    expect(component.editor()!.kraiName).toBe('Пласт Чехія');
    expect(component.renamed()).toBe(false);
  });
});
