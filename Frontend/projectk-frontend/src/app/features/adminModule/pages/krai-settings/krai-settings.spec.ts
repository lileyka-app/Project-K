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
  let profiles: jasmine.SpyObj<KraiProfileService>;
  let messages: MessageService;

  const czProfile: KraiProfileEditorDto = {
    kraiCode: 'cz',
    kraiName: 'Чехія',
    revision: 2,
    document: { catalog: { badgeSources: ['ua'], hiddenBadges: ['ua-2'] }, locale: { currency: 'CZK' } }
  };

  beforeEach(async () => {
    profiles = jasmine.createSpyObj<KraiProfileService>('KraiProfileService', ['create', 'rename', 'get', 'save', 'revisions', 'restore', 'concessionReport']);
    profiles.concessionReport.and.returnValue(of({ kraiCode: 'cz', kraiName: 'Чехія', currency: 'CZK', numberLocale: 'uk-UA', quarter: { year: 2026, number: 4 }, kurinCount: 1, concessions: [] }));
    profiles.get.and.returnValue(of(czProfile));
    profiles.revisions.and.returnValue(of([
      { revision: 2, createdAtUtc: '2026-10-14T10:00:00Z', createdByName: 'Адмін', note: 'Українські вмілості' },
      { revision: 1, createdAtUtc: '2026-10-13T10:00:00Z', createdByName: null, note: 'Початковий профіль' }
    ]));
    const kurins = jasmine.createSpyObj<KurinService>('KurinService', ['getKrais']);
    kurins.getKrais.and.returnValue(of([{ code: 'cz', name: 'Чехія', kurinCount: 1 }]));
    const catalog = jasmine.createSpyObj<BadgesCatalogService>('BadgesCatalogService', ['getAll']);
    catalog.getAll.and.returnValue(of([badge('ua-1', 'ua', 'Україна'), badge('ua-2', 'ua', 'Україна'), badge('pl-1', 'pl', 'Польща')]));

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
    expect(component.isOffered('ua-2')).toBeFalse();
    expect(component.sourceOptions().map(o => o.code)).toEqual(['pl', 'ua']);
    expect(fixture.nativeElement.textContent).toContain('Ревізія 2');
  });

  it('saves a new revision on top of the one it loaded, keeping the other sections', () => {
    profiles.save.and.returnValue(of({ ...czProfile, revision: 3, document: { catalog: { badgeSources: ['ua'] }, locale: { currency: 'CZK' } } }));

    component.setOffered('ua-2', true);
    expect(component.dirty()).toBeTrue();
    component.note.set(' усе українське ');
    component.save();

    expect(profiles.save).toHaveBeenCalledWith('cz', 2, {
      catalog: { badgeSources: ['ua'], hiddenBadges: undefined },
      locale: { currency: 'CZK', timeZone: undefined, numberLocale: undefined },
      dues: undefined,
      cardFields: undefined
    }, 'усе українське');
    expect(component.editor()!.revision).toBe(3);
    expect(component.dirty()).toBeFalse();
  });

  it('shows the server explanation when someone saved first', () => {
    spyOn(messages, 'add');
    profiles.save.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { error: 'ProfileChanged', message: 'Профіль змінили, поки ти його редагував. Онови сторінку.' }
    })));

    component.setOffered('ua-1', false);
    component.save();

    expect(messages.add).toHaveBeenCalledWith(jasmine.objectContaining({ detail: 'Профіль змінили, поки ти його редагував. Онови сторінку.' }));
  });

  it('saves the currency, number format and time zone, leaving what it does not edit', () => {
    profiles.save.and.returnValue(of(czProfile));

    expect(component.currency()).toBe('CZK');
    expect(component.timeZone()).toBeNull();
    expect(component.numberLocale()).toBeNull();
    component.timeZone.set('Europe/Prague');
    component.numberLocale.set('cs-CZ');
    component.upstream.set(DuesUpstream.Krai);
    expect(component.dirty()).toBeTrue();
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
    profiles.save.and.returnValue(of(czProfile));
    const concessions = [{ key: 'p-sib', name: 'Брат чи сестра', kind: 'percent' as const, value: 20, appliesTo: ['stanytsia' as const] }];

    component.concessions.set(concessions);
    expect(component.dirty()).toBeTrue();
    component.save();

    expect(profiles.save).toHaveBeenCalledWith('cz', 2, jasmine.objectContaining({ dues: jasmine.objectContaining({ concessions }) }), null);
    expect(fixture.nativeElement.textContent).toContain('Пільги краю');
  });

  it('saves the card fields with the rest of the profile', () => {
    profiles.save.and.returnValue(of(czProfile));
    const cardFields = {
      fields: { middleName: 'hidden' as const },
      extra: [{ key: 'visaType', label: 'Тип візи', type: 'text' as const, required: true }]
    };

    component.cardFields.set(cardFields);
    expect(component.dirty()).toBeTrue();
    component.save();

    expect(profiles.save).toHaveBeenCalledWith('cz', 2, jasmine.objectContaining({ cardFields }), null);
    expect(fixture.nativeElement.textContent).toContain('Картка члена');
  });

  it('restores an earlier revision', () => {
    profiles.restore.and.returnValue(of({ ...czProfile, revision: 3, document: {} }));

    component.restore(component.revisions()[1]);

    expect(profiles.restore).toHaveBeenCalledWith('cz', 1);
    expect(component.sources()).toEqual([]);
    expect(component.effectiveSources()).toEqual(['cz']);
  });

  it('adds a край and opens its profile', () => {
    profiles.create.and.returnValue(of({ code: 'sk', name: 'Словаччина', kurinCount: 0 }));
    profiles.get.and.returnValue(of({ kraiCode: 'sk', kraiName: 'Словаччина', revision: 1, document: { catalog: { badgeSources: ['ua'] } } }));

    component.startAdding();
    component.newCode.set(' SK ');
    component.newName.set(' Словаччина ');
    expect(component.newCodeValid()).toBeTrue();
    component.addKrai();

    expect(profiles.create).toHaveBeenCalledWith('sk', 'Словаччина');
    expect(component.adding()).toBeFalse();
    expect(component.krais().map(k => k.code)).toEqual(['sk', 'cz']);
    expect(component.kraiCode()).toBe('sk');
    expect(component.editor()!.kraiName).toBe('Словаччина');
  });

  it('does not send a code that cannot name a край', () => {
    component.startAdding();
    component.newCode.set('sk-1');
    component.newName.set('Словаччина');
    component.addKrai();

    expect(component.newCodeValid()).toBeFalse();
    expect(profiles.create).not.toHaveBeenCalled();
  });

  it('renames the край, keeping its code', () => {
    profiles.rename.and.returnValue(of({ code: 'cz', name: 'Пласт Чехія', kurinCount: 1 }));

    expect(component.renamed()).toBeFalse();
    component.name.set('Пласт Чехія');
    component.renameKrai();

    expect(profiles.rename).toHaveBeenCalledWith('cz', 'Пласт Чехія');
    expect(component.krais()[0].name).toBe('Пласт Чехія');
    expect(component.editor()!.kraiName).toBe('Пласт Чехія');
    expect(component.renamed()).toBeFalse();
  });
});
